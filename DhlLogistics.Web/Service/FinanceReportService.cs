namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Read-only reporting over Posted vouchers + Approved/Closed bills.
///
/// Sign convention for ledger balances:
///   +ve = Dr balance, −ve = Cr balance.
/// So when computing a balance:
///   delta = line.Dr − line.Cr.
/// Opening balance from <see cref="AccountHead"/> is converted to signed by
/// multiplying with +1 (Debit) or −1 (Credit).
/// </summary>
public class FinanceReportService
{
    private readonly AppDbContext _db;
    public FinanceReportService(AppDbContext db) => _db = db;

    private static decimal Signed(decimal amount, DrCr type) =>
        type == DrCr.Debit ? amount : -amount;

    // ── Ledger ───────────────────────────────────────────────────────────────

    public async Task<LedgerReport> GetLedgerAsync(
        int accountHeadId, DateTime fromDate, DateTime toDate, int? branchId = null)
    {
        var head = await _db.AccountHeads.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountHeadId)
            ?? throw new KeyNotFoundException($"Account head {accountHeadId} not found.");

        // Only Posted vouchers contribute to the ledger.
        IQueryable<VoucherLine> q = _db.VoucherLines.AsNoTracking()
            .Include(l => l.Voucher).ThenInclude(v => v!.Lines).ThenInclude(l => l.AccountHead)
            .Where(l => l.AccountHeadId == accountHeadId
                     && l.Voucher!.Status == VoucherStatus.Posted);

        if (branchId.HasValue) q = q.Where(l => l.Voucher!.BranchId == branchId);

        // Opening (everything posted strictly before fromDate)
        var beforeFrom = await q.Where(l => l.Voucher!.VoucherDate < fromDate.Date)
            .Select(l => new { l.DrCr, l.Amount })
            .ToListAsync();

        decimal opening = Signed(head.OpeningBalance, head.OpeningBalanceType)
                        + beforeFrom.Sum(x => Signed(x.Amount, x.DrCr));

        // Period
        var rangeLines = await q
            .Where(l => l.Voucher!.VoucherDate >= fromDate.Date
                     && l.Voucher!.VoucherDate <= toDate.Date)
            .OrderBy(l => l.Voucher!.VoucherDate)
            .ThenBy(l => l.VoucherId)
            .ThenBy(l => l.DisplayOrder)
            .ToListAsync();

        decimal running = opening;
        decimal periodDr = 0, periodCr = 0;
        var entries = new List<LedgerEntry>(rangeLines.Count);

        foreach (var l in rangeLines)
        {
            var v = l.Voucher!;
            decimal dr = l.DrCr == DrCr.Debit  ? l.Amount : 0;
            decimal cr = l.DrCr == DrCr.Credit ? l.Amount : 0;
            running += dr - cr;
            periodDr += dr;
            periodCr += cr;

            // For a simple 2-line voucher, the "contra" account is the other side.
            string? contra = null;
            if (v.Lines.Count == 2)
            {
                var other = v.Lines.FirstOrDefault(x => x.AccountHeadId != accountHeadId);
                contra = other?.AccountHead?.AccountName;
            }
            else if (v.Lines.Count > 2)
            {
                contra = "(split)";
            }

            entries.Add(new LedgerEntry
            {
                Date          = v.VoucherDate,
                VoucherNo     = v.VoucherNo,
                VoucherType   = v.Type,
                Narration     = string.IsNullOrWhiteSpace(l.Narration) ? v.Narration : l.Narration,
                Reference     = v.ReferenceNo,
                ContraAccount = contra,
                Debit         = dr,
                Credit        = cr,
                RunningBalance = running,
            });
        }

        return new LedgerReport
        {
            AccountHeadId  = head.Id,
            AccountCode    = head.AccountCode,
            AccountName    = head.AccountName,
            Group          = head.Group,
            FromDate       = fromDate.Date,
            ToDate         = toDate.Date,
            OpeningBalance = opening,
            PeriodDebit    = periodDr,
            PeriodCredit   = periodCr,
            ClosingBalance = running,
            Entries        = entries,
        };
    }

    // ── Trial Balance ────────────────────────────────────────────────────────

    public async Task<List<TrialBalanceRow>> GetTrialBalanceAsync(
        DateTime fromDate, DateTime toDate, int? branchId = null)
    {
        var heads = await _db.AccountHeads.AsNoTracking()
            .OrderBy(a => a.Group).ThenBy(a => a.AccountCode)
            .ToListAsync();

        IQueryable<VoucherLine> q = _db.VoucherLines.AsNoTracking()
            .Where(l => l.Voucher!.Status == VoucherStatus.Posted);

        if (branchId.HasValue) q = q.Where(l => l.Voucher!.BranchId == branchId);

        var before = await q
            .Where(l => l.Voucher!.VoucherDate < fromDate.Date)
            .GroupBy(l => l.AccountHeadId)
            .Select(g => new
            {
                AccountHeadId = g.Key,
                Dr = g.Where(x => x.DrCr == DrCr.Debit ).Sum(x => (decimal?)x.Amount) ?? 0,
                Cr = g.Where(x => x.DrCr == DrCr.Credit).Sum(x => (decimal?)x.Amount) ?? 0,
            }).ToListAsync();

        var period = await q
            .Where(l => l.Voucher!.VoucherDate >= fromDate.Date
                     && l.Voucher!.VoucherDate <= toDate.Date)
            .GroupBy(l => l.AccountHeadId)
            .Select(g => new
            {
                AccountHeadId = g.Key,
                Dr = g.Where(x => x.DrCr == DrCr.Debit ).Sum(x => (decimal?)x.Amount) ?? 0,
                Cr = g.Where(x => x.DrCr == DrCr.Credit).Sum(x => (decimal?)x.Amount) ?? 0,
            }).ToListAsync();

        var beforeMap = before.ToDictionary(x => x.AccountHeadId);
        var periodMap = period.ToDictionary(x => x.AccountHeadId);

        var rows = new List<TrialBalanceRow>(heads.Count);
        foreach (var h in heads)
        {
            decimal openingSigned = Signed(h.OpeningBalance, h.OpeningBalanceType);
            if (beforeMap.TryGetValue(h.Id, out var b))
                openingSigned += b.Dr - b.Cr;

            decimal pDr = 0, pCr = 0;
            if (periodMap.TryGetValue(h.Id, out var p))
            {
                pDr = p.Dr;
                pCr = p.Cr;
            }

            decimal closingSigned = openingSigned + (pDr - pCr);

            // skip rows with zero activity AND zero balance to keep the report tight
            if (openingSigned == 0 && pDr == 0 && pCr == 0 && closingSigned == 0) continue;

            rows.Add(new TrialBalanceRow
            {
                AccountHeadId = h.Id,
                AccountCode   = h.AccountCode,
                AccountName   = h.AccountName,
                Group         = h.Group,
                OpeningDebit  = openingSigned > 0 ? openingSigned  : 0,
                OpeningCredit = openingSigned < 0 ? -openingSigned : 0,
                PeriodDebit   = pDr,
                PeriodCredit  = pCr,
                ClosingDebit  = closingSigned > 0 ? closingSigned  : 0,
                ClosingCredit = closingSigned < 0 ? -closingSigned : 0,
            });
        }

        return rows;
    }

    // ── GST Output Register ──────────────────────────────────────────────────

    public async Task<List<GstOutputRow>> GetGstOutputAsync(
        DateTime fromDate, DateTime toDate, BillMode? mode = null, int? branchId = null)
    {
        IQueryable<Bill> q = _db.Bills.AsNoTracking()
            .Include(b => b.BillingClient)
            .Include(b => b.Branch)
            .Include(b => b.Charges)
            .Where(b => b.BillDate >= fromDate.Date
                     && b.BillDate <= toDate.Date
                     && (b.Status == BillStatus.Approved || b.Status == BillStatus.Closed)
                     && b.GstAmount > 0);

        if (mode.HasValue)     q = q.Where(b => b.Mode == mode.Value);
        if (branchId.HasValue) q = q.Where(b => b.BranchId == branchId);

        var bills = await q.OrderBy(b => b.BillDate).ThenBy(b => b.BillNo).ToListAsync();

        return bills.Select(b => new GstOutputRow
        {
            BillId       = b.Id,
            BillNo       = b.BillNo,
            BillDate     = b.BillDate,
            Mode         = b.Mode,
            ClientName   = b.BillingClient?.CompanyName,
            Branch       = b.Branch?.BranchName,
            TaxableValue = b.SubTotal,
            GstAmount    = b.GstAmount,
            GstRate      = b.SubTotal > 0 ? decimal.Round(b.GstAmount * 100m / b.SubTotal, 2) : 0,
            TotalAmount  = b.TotalAmount,
            Status       = b.Status,
        }).ToList();
    }

    // ── Combined Bill Register ───────────────────────────────────────────────

    public async Task<List<BillRegisterRow>> GetBillRegisterAsync(
        DateTime fromDate, DateTime toDate,
        BillMode? mode = null, BillStatus? status = null, int? branchId = null)
    {
        IQueryable<Bill> q = _db.Bills.AsNoTracking()
            .Include(b => b.BillingClient)
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.JobOrder)
            .Where(b => b.BillDate >= fromDate.Date && b.BillDate <= toDate.Date);

        if (mode.HasValue)     q = q.Where(b => b.Mode == mode.Value);
        if (status.HasValue)   q = q.Where(b => b.Status == status.Value);
        else                   q = q.Where(b => b.Status != BillStatus.Draft);  // default: skip drafts
        if (branchId.HasValue) q = q.Where(b => b.BranchId == branchId);

        var bills = await q.OrderBy(b => b.BillDate).ThenBy(b => b.BillNo).ToListAsync();

        return bills.Select(b => new BillRegisterRow
        {
            BillId       = b.Id,
            BillNo       = b.BillNo,
            BillDate     = b.BillDate,
            Mode         = b.Mode,
            ClientName   = b.BillingClient?.CompanyName,
            Branch       = b.Branch?.BranchName,
            JobOrderNo   = b.JobOrder?.JobOrderNo,
            Currency     = b.Currency?.CurrencyCode,
            ExchangeRate = b.ExchangeRate,
            SubTotal     = b.SubTotal,
            GstAmount    = b.GstAmount,
            TotalAmount  = b.TotalAmount,
            Status       = b.Status,
            CreatedBy    = b.CreatedBy,
            ApprovedBy   = b.ApprovedBy,
        }).ToList();
    }
}
