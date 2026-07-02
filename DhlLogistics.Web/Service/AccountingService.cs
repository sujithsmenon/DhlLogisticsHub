namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The automatic accounting engine. Turns operational events into balanced, already-Posted double-entry
/// vouchers so nothing is posted by hand:
/// <list type="bullet">
///   <item><b>Bill approval</b> → sales entry: Dr Accounts Receivable, Cr Revenue (by mode), Cr GST Payable.</item>
///   <item><b>Job approval / edit</b> → per-operation cost: Dr Expense (by category), Cr Accounts Payable (vendor).</item>
///   <item><b>Customer payment</b> → Receipt: Dr Cash/Bank, Cr Accounts Receivable.</item>
///   <item><b>Vendor payment</b> → Payment: Dr Accounts Payable, Cr Cash/Bank.</item>
/// </list>
/// All vouchers are built here and persisted through <see cref="VoucherService.CreatePostedAsync"/> — one
/// copy of the numbering/balance/post logic. Heads are resolved by the stable <see cref="AccountSeed.Codes"/>
/// so seed and posting can never drift. Every auto-posting is idempotent (keyed on <c>Voucher.ReferenceNo</c>)
/// so re-approval, edit-after-approval, backfill and repeated startups never double-post.
/// Posting methods (<see cref="PostForBillAsync"/>, <see cref="PostJobExpensesAsync"/>) run inside the
/// caller's transaction; payment methods own their own transaction.
/// </summary>
public class AccountingService
{
    private readonly AppDbContext _db;
    private readonly VoucherService _vouchers;
    private readonly DashboardState _dash;
    private readonly AuthenticationStateProvider _authProvider;

    public AccountingService(AppDbContext db, VoucherService vouchers, DashboardState dash,
                             AuthenticationStateProvider authProvider)
    {
        _db = db;
        _vouchers = vouchers;
        _dash = dash;
        _authProvider = authProvider;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Head resolution (by stable code — throws so misconfig surfaces, never silent) ─────────
    private async Task<AccountHead> HeadAsync(string code) =>
        await _db.AccountHeads.FirstOrDefaultAsync(a => a.AccountCode == code)
        ?? throw new InvalidOperationException(
            $"Chart of Accounts is missing head '{code}'. Restart to re-seed, or add it under Accounts.");

    private static string RevenueCode(BillMode mode) => mode switch
    {
        BillMode.Clearance      => AccountSeed.Codes.ClearanceRevenue,
        BillMode.Forwarding     => AccountSeed.Codes.ForwardingRevenue,
        BillMode.Transportation => AccountSeed.Codes.TransportationRevenue,
        _                       => AccountSeed.Codes.OtherServiceRevenue,
    };

    private static string ExpenseCode(ChargeCategory cat) => cat switch
    {
        ChargeCategory.Transport     => AccountSeed.Codes.TransportationExpense,
        ChargeCategory.Customs       => AccountSeed.Codes.CustomsDuty,
        ChargeCategory.Port          => AccountSeed.Codes.PortCharges,
        ChargeCategory.Labour        => AccountSeed.Codes.LabourCharges,
        ChargeCategory.Documentation => AccountSeed.Codes.DocumentationCharges,
        _                            => AccountSeed.Codes.MiscellaneousExpense,
    };

    // ── 1. Bill approval → revenue / receivable ───────────────────────────────────────────────
    /// <summary>
    /// Posts the sales entry for an Approved bill (Dr A/R, Cr Revenue, Cr GST Payable). No-op when the
    /// bill has nothing to bill (Total ≤ 0, e.g. a still-Pending Transportation bill) or was already
    /// posted. Runs inside the caller's transaction.
    /// </summary>
    public async Task PostForBillAsync(long billId, string actor)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId);
        if (bill is null || bill.Status != BillStatus.Approved) return;
        if (bill.TotalAmount <= 0) return;                                   // nothing to post yet

        var reference = bill.BillNo;
        if (await _db.Vouchers.AnyAsync(v => v.ReferenceNo == reference && v.Type == VoucherType.Journal))
            return;                                                          // idempotent

        var ar      = await HeadAsync(AccountSeed.Codes.AccountsReceivable);
        var revenue = await HeadAsync(RevenueCode(bill.Mode));

        var v = new Voucher
        {
            Type        = VoucherType.Journal,
            VoucherDate = bill.BillDate,
            BranchId    = bill.BranchId,
            PartyId     = bill.BillingClientId,
            ReferenceNo = reference,
            Narration   = $"Auto-posted from {bill.Mode} bill {bill.BillNo}.",
        };
        v.Lines.Add(new VoucherLine { AccountHeadId = ar.Id, DrCr = DrCr.Debit, Amount = bill.TotalAmount, Narration = "Customer receivable" });
        v.Lines.Add(new VoucherLine { AccountHeadId = revenue.Id, DrCr = DrCr.Credit, Amount = bill.SubTotal, Narration = $"{bill.Mode} revenue" });
        if (bill.GstAmount != 0)
        {
            var gst = await HeadAsync(AccountSeed.Codes.GstPayable);
            v.Lines.Add(new VoucherLine { AccountHeadId = gst.Id, DrCr = DrCr.Credit, Amount = bill.GstAmount, Narration = "GST output payable" });
        }

        await _vouchers.CreatePostedAsync(v, actor);
    }

    // ── 2. Job approval / edit → vendor expense / payable ─────────────────────────────────────
    /// <summary>
    /// Posts each job operation's vendor cost (Dr Expense by category, Cr Accounts Payable) for operations
    /// that have both a cost and a vendor (<see cref="JobOrderOperation.OperatedByClientId"/>). Operations
    /// costed later (transport, customs …) post on the next job save. Idempotent per operation. Runs inside
    /// the caller's transaction.
    /// </summary>
    public async Task PostJobExpensesAsync(JobOrder job, string actor)
    {
        var ops = await _db.JobOrderOperations
            .Where(o => o.JobOrderId == job.Id && o.OperatedByClientId != null && o.Cost != null && o.Cost > 0)
            .ToListAsync();

        var ap = ops.Count > 0 ? await HeadAsync(AccountSeed.Codes.AccountsPayable) : null;

        foreach (var op in ops)
        {
            var reference = $"{job.JobOrderNo}#OP{op.Id}";
            if (await _db.Vouchers.AnyAsync(v => v.ReferenceNo == reference && v.Type == VoucherType.Journal))
                continue;                                                    // idempotent

            var expense = await HeadAsync(ExpenseCode(op.ExpenseCategory));
            var cost    = op.Cost!.Value;

            var v = new Voucher
            {
                Type        = VoucherType.Journal,
                VoucherDate = job.JobOrderDate,
                BranchId    = job.BranchId,
                PartyId     = op.OperatedByClientId,
                ReferenceNo = reference,
                Narration   = $"Vendor cost for '{op.OperationName}' on job {job.JobOrderNo}.",
            };
            v.Lines.Add(new VoucherLine { AccountHeadId = expense.Id, DrCr = DrCr.Debit,  Amount = cost, Narration = op.OperationName });
            v.Lines.Add(new VoucherLine { AccountHeadId = ap!.Id,     DrCr = DrCr.Credit, Amount = cost, Narration = "Vendor payable" });

            await _vouchers.CreatePostedAsync(v, actor);
        }
    }

    // ── 3. Customer payment → Receipt (money in) ──────────────────────────────────────────────
    /// <summary>Records a customer receipt: Dr Cash/Bank, Cr Accounts Receivable. Opens its own transaction.</summary>
    public async Task<Voucher> RecordCustomerPaymentAsync(
        int customerId, decimal amount, int cashOrBankHeadId, DateTime date,
        string? reference = null, int? branchId = null, string? actor = null)
    {
        if (amount <= 0) throw new InvalidOperationException("Payment amount must be greater than 0.");
        var user = actor ?? await CurrentUserAsync();

        var bank = await _db.AccountHeads.FirstOrDefaultAsync(a => a.Id == cashOrBankHeadId)
            ?? throw new InvalidOperationException("Select a valid Cash/Bank account.");
        var ar = await HeadAsync(AccountSeed.Codes.AccountsReceivable);

        var v = new Voucher
        {
            Type                = VoucherType.Receipt,
            VoucherDate         = date,
            BranchId            = branchId,
            CashOrBankAccountId = bank.Id,
            PartyId             = customerId,
            ReferenceNo         = reference,
            Narration           = $"Customer receipt via {bank.AccountName}.",
        };
        v.Lines.Add(new VoucherLine { AccountHeadId = bank.Id, DrCr = DrCr.Debit,  Amount = amount, Narration = "Money received" });
        v.Lines.Add(new VoucherLine { AccountHeadId = ar.Id,   DrCr = DrCr.Credit, Amount = amount, Narration = "Settle receivable" });

        await using var tx = await _db.Database.BeginTransactionAsync();
        var posted = await _vouchers.CreatePostedAsync(v, user);
        await tx.CommitAsync();
        _dash.NotifyChanged();
        return posted;
    }

    // ── 4. Vendor payment → Payment (money out) ───────────────────────────────────────────────
    /// <summary>Records a vendor payment: Dr Accounts Payable, Cr Cash/Bank. Opens its own transaction.</summary>
    public async Task<Voucher> RecordVendorPaymentAsync(
        int vendorId, decimal amount, int cashOrBankHeadId, DateTime date,
        string? reference = null, int? branchId = null, string? actor = null)
    {
        if (amount <= 0) throw new InvalidOperationException("Payment amount must be greater than 0.");
        var user = actor ?? await CurrentUserAsync();

        var bank = await _db.AccountHeads.FirstOrDefaultAsync(a => a.Id == cashOrBankHeadId)
            ?? throw new InvalidOperationException("Select a valid Cash/Bank account.");
        var ap = await HeadAsync(AccountSeed.Codes.AccountsPayable);

        var v = new Voucher
        {
            Type                = VoucherType.Payment,
            VoucherDate         = date,
            BranchId            = branchId,
            CashOrBankAccountId = bank.Id,
            PartyId             = vendorId,
            ReferenceNo         = reference,
            Narration           = $"Vendor payment via {bank.AccountName}.",
        };
        v.Lines.Add(new VoucherLine { AccountHeadId = ap.Id,   DrCr = DrCr.Debit,  Amount = amount, Narration = "Settle payable" });
        v.Lines.Add(new VoucherLine { AccountHeadId = bank.Id, DrCr = DrCr.Credit, Amount = amount, Narration = "Money paid" });

        await using var tx = await _db.Database.BeginTransactionAsync();
        var posted = await _vouchers.CreatePostedAsync(v, user);
        await tx.CommitAsync();
        _dash.NotifyChanged();
        return posted;
    }
}
