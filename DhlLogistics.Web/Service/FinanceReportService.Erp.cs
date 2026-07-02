namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// ERP reporting extensions for <see cref="FinanceReportService"/>. Every method is READ-ONLY and
/// computes from live data at query time — nothing is ever stored. Sources:
///   • Accounts (P&amp;L, Balance Sheet, Cash/Bank Book, statements, aging) → <b>Posted</b> vouchers.
///   • Billing/ops reports (Job Profitability, mode reports) → Approved/Closed bills + job operations.
/// All monetary aggregation reuses the same sign convention and the shared <c>Signed</c> helper as the
/// original ledger/trial-balance methods, so there is one copy of the balance logic.
/// </summary>
public partial class FinanceReportService
{
    // Base query: every Posted voucher line, optionally scoped to a branch + date ceiling.
    private IQueryable<VoucherLine> PostedLines(int? branchId = null, DateTime? upto = null)
    {
        var q = _db.VoucherLines.AsNoTracking()
            .Where(l => l.Voucher!.Status == VoucherStatus.Posted);
        if (branchId.HasValue) q = q.Where(l => l.Voucher!.BranchId == branchId);
        if (upto.HasValue)     q = q.Where(l => l.Voucher!.VoucherDate <= upto.Value.Date);
        return q;
    }

    private Task<AccountHead?> HeadByCodeAsync(string code) =>
        _db.AccountHeads.AsNoTracking().FirstOrDefaultAsync(a => a.AccountCode == code);

    // ── 1. Profit & Loss ──────────────────────────────────────────────────────
    public async Task<ProfitAndLossReport> GetProfitAndLossAsync(DateTime from, DateTime to, int? branchId = null)
    {
        var heads = await _db.AccountHeads.AsNoTracking()
            .Where(a => a.Group == AccountGroup.Income || a.Group == AccountGroup.Expense)
            .OrderBy(a => a.AccountCode)
            .ToListAsync();
        var headIds = heads.Select(h => h.Id).ToList();

        var q = PostedLines(branchId)
            .Where(l => l.Voucher!.VoucherDate >= from.Date && l.Voucher!.VoucherDate <= to.Date
                     && headIds.Contains(l.AccountHeadId));

        var agg = (await q.GroupBy(l => l.AccountHeadId)
            .Select(g => new
            {
                AccountHeadId = g.Key,
                Dr = g.Where(x => x.DrCr == DrCr.Debit ).Sum(x => (decimal?)x.Amount) ?? 0,
                Cr = g.Where(x => x.DrCr == DrCr.Credit).Sum(x => (decimal?)x.Amount) ?? 0,
            }).ToListAsync()).ToDictionary(x => x.AccountHeadId);

        var report = new ProfitAndLossReport { FromDate = from.Date, ToDate = to.Date };
        foreach (var h in heads)
        {
            agg.TryGetValue(h.Id, out var a);
            decimal dr = a?.Dr ?? 0, cr = a?.Cr ?? 0;
            if (h.Group == AccountGroup.Income)
            {
                var amt = cr - dr;                                   // income normal = credit
                if (amt != 0) report.Revenue.Add(new PLLine { AccountCode = h.AccountCode, AccountName = h.AccountName, Amount = amt });
            }
            else
            {
                var amt = dr - cr;                                   // expense normal = debit
                if (amt == 0) continue;
                var line = new PLLine { AccountCode = h.AccountCode, AccountName = h.AccountName, Amount = amt };
                // Operational cost heads are direct (COGS); Miscellaneous is treated as indirect/overhead.
                if (h.AccountCode == AccountSeed.Codes.MiscellaneousExpense) report.IndirectCosts.Add(line);
                else                                                          report.DirectCosts.Add(line);
            }
        }

        report.TotalRevenue      = report.Revenue.Sum(x => x.Amount);
        report.TotalDirectCost   = report.DirectCosts.Sum(x => x.Amount);
        report.TotalIndirectCost = report.IndirectCosts.Sum(x => x.Amount);
        report.GrossProfit       = report.TotalRevenue - report.TotalDirectCost;
        report.NetProfit         = report.GrossProfit - report.TotalIndirectCost;
        return report;
    }

    // ── 2. Balance Sheet ──────────────────────────────────────────────────────
    public async Task<BalanceSheetReport> GetBalanceSheetAsync(DateTime asOf, int? branchId = null)
    {
        var heads = await _db.AccountHeads.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();

        var agg = (await PostedLines(branchId, asOf).GroupBy(l => l.AccountHeadId)
            .Select(g => new
            {
                AccountHeadId = g.Key,
                Dr = g.Where(x => x.DrCr == DrCr.Debit ).Sum(x => (decimal?)x.Amount) ?? 0,
                Cr = g.Where(x => x.DrCr == DrCr.Credit).Sum(x => (decimal?)x.Amount) ?? 0,
            }).ToListAsync()).ToDictionary(x => x.AccountHeadId);

        var report = new BalanceSheetReport { AsOfDate = asOf.Date };
        decimal income = 0, expense = 0;

        foreach (var h in heads)
        {
            agg.TryGetValue(h.Id, out var a);
            decimal signed = Signed(h.OpeningBalance, h.OpeningBalanceType) + ((a?.Dr ?? 0) - (a?.Cr ?? 0));
            switch (h.Group)
            {
                case AccountGroup.Asset:
                    if (signed != 0) report.Assets.Add(new() { AccountCode = h.AccountCode, AccountName = h.AccountName, Amount = signed });
                    break;
                case AccountGroup.Liability:
                    if (signed != 0) report.Liabilities.Add(new() { AccountCode = h.AccountCode, AccountName = h.AccountName, Amount = -signed });
                    break;
                case AccountGroup.Equity:
                    if (signed != 0) report.Equity.Add(new() { AccountCode = h.AccountCode, AccountName = h.AccountName, Amount = -signed });
                    break;
                case AccountGroup.Income:  income  += -signed; break;   // credit-normal
                case AccountGroup.Expense: expense += signed;  break;   // debit-normal
            }
        }

        report.NetProfit = income - expense;
        if (report.NetProfit != 0)
            report.Equity.Add(new() { AccountCode = "", AccountName = "Retained Earnings / Net Profit", Amount = report.NetProfit });

        report.TotalAssets      = report.Assets.Sum(x => x.Amount);
        report.TotalLiabilities = report.Liabilities.Sum(x => x.Amount);
        report.TotalEquity      = report.Equity.Sum(x => x.Amount);
        return report;
    }

    // ── 3. Revenue / Expense by period ────────────────────────────────────────
    public Task<List<PeriodAmountRow>> GetRevenueReportAsync(DateTime from, DateTime to, ReportGranularity g, int? branchId = null) =>
        PeriodReportAsync(AccountGroup.Income, credit: true, from, to, g, branchId);

    public Task<List<PeriodAmountRow>> GetExpenseReportAsync(DateTime from, DateTime to, ReportGranularity g, int? branchId = null) =>
        PeriodReportAsync(AccountGroup.Expense, credit: false, from, to, g, branchId);

    private async Task<List<PeriodAmountRow>> PeriodReportAsync(
        AccountGroup group, bool credit, DateTime from, DateTime to, ReportGranularity g, int? branchId)
    {
        var headIds = await _db.AccountHeads.AsNoTracking()
            .Where(a => a.Group == group).Select(a => a.Id).ToListAsync();

        var lines = await PostedLines(branchId)
            .Where(l => l.Voucher!.VoucherDate >= from.Date && l.Voucher!.VoucherDate <= to.Date
                     && headIds.Contains(l.AccountHeadId))
            .Select(l => new { l.Voucher!.VoucherDate, l.DrCr, l.Amount })
            .ToListAsync();

        // Income increases with Cr, expense with Dr.
        var signed = lines.Select(x => (x.VoucherDate, Amount: credit
            ? (x.DrCr == DrCr.Credit ? x.Amount : -x.Amount)
            : (x.DrCr == DrCr.Debit  ? x.Amount : -x.Amount)));

        DateTime Bucket(DateTime d) => g switch
        {
            ReportGranularity.Daily   => d.Date,
            ReportGranularity.Monthly => new DateTime(d.Year, d.Month, 1),
            _                         => new DateTime(d.Year, 1, 1),
        };
        string Label(DateTime d) => g switch
        {
            ReportGranularity.Daily   => d.ToString("dd-MMM-yyyy"),
            ReportGranularity.Monthly => d.ToString("MMM-yyyy"),
            _                         => d.ToString("yyyy"),
        };

        return signed
            .GroupBy(x => Bucket(x.VoucherDate))
            .OrderBy(gr => gr.Key)
            .Select(gr => new PeriodAmountRow { PeriodStart = gr.Key, Period = Label(gr.Key), Amount = gr.Sum(x => x.Amount) })
            .ToList();
    }

    // ── 4. Cash Book / Bank Book (reuse the ledger) ───────────────────────────
    public async Task<LedgerReport?> GetCashBookAsync(DateTime from, DateTime to, int? branchId = null)
    {
        var cash = await _db.AccountHeads.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsCash && a.IsActive);
        return cash is null ? null : await GetLedgerAsync(cash.Id, from, to, branchId);
    }

    public async Task<LedgerReport?> GetBankBookAsync(DateTime from, DateTime to, int? branchId = null)
    {
        var bank = await _db.AccountHeads.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsBank && a.IsActive);
        return bank is null ? null : await GetLedgerAsync(bank.Id, from, to, branchId);
    }

    // ── 5. Payment Register (Receipt + Payment vouchers) ──────────────────────
    public async Task<List<PaymentRegisterRow>> GetPaymentRegisterAsync(
        DateTime from, DateTime to, VoucherType? type = null, int? branchId = null, int? partyId = null)
    {
        var q = _db.Vouchers.AsNoTracking()
            .Include(v => v.CashOrBankAccount)
            .Include(v => v.Party)
            .Where(v => v.Status == VoucherStatus.Posted
                     && (v.Type == VoucherType.Receipt || v.Type == VoucherType.Payment)
                     && v.VoucherDate >= from.Date && v.VoucherDate <= to.Date);

        if (type.HasValue)     q = q.Where(v => v.Type == type.Value);
        if (branchId.HasValue) q = q.Where(v => v.BranchId == branchId);
        if (partyId.HasValue)  q = q.Where(v => v.PartyId == partyId);

        var vs = await q.OrderBy(v => v.VoucherDate).ThenBy(v => v.VoucherNo).ToListAsync();

        return vs.Select(v => new PaymentRegisterRow
        {
            VoucherId = v.Id,
            Date      = v.VoucherDate,
            VoucherNo = v.VoucherNo,
            Type      = v.Type,
            Party     = v.Party?.CompanyName,
            CashBank  = v.CashOrBankAccount?.AccountName,
            Amount    = v.TotalDebit,                       // balanced → Dr total = Cr total
            Reference = v.ReferenceNo,
            Narration = v.Narration,
        }).ToList();
    }

    // ── 6. Customer / Vendor statement (party ledger on A/R or A/P) ───────────
    public Task<PartyStatementReport> GetCustomerStatementAsync(int customerId, DateTime from, DateTime to, int? branchId = null) =>
        PartyStatementAsync(customerId, AccountSeed.Codes.AccountsReceivable, creditNormal: false, from, to, branchId);

    public Task<PartyStatementReport> GetVendorStatementAsync(int vendorId, DateTime from, DateTime to, int? branchId = null) =>
        PartyStatementAsync(vendorId, AccountSeed.Codes.AccountsPayable, creditNormal: true, from, to, branchId);

    private async Task<PartyStatementReport> PartyStatementAsync(
        int partyId, string headCode, bool creditNormal, DateTime from, DateTime to, int? branchId)
    {
        var report = new PartyStatementReport { PartyId = partyId, FromDate = from.Date, ToDate = to.Date };
        report.PartyName = await _db.Clients.AsNoTracking().Where(c => c.Id == partyId)
            .Select(c => c.CompanyName).FirstOrDefaultAsync();

        var head = await HeadByCodeAsync(headCode);
        if (head is null) return report;                    // chart of accounts not seeded yet

        var baseQ = _db.VoucherLines.AsNoTracking()
            .Where(l => l.AccountHeadId == head.Id
                     && l.Voucher!.Status == VoucherStatus.Posted
                     && l.Voucher!.PartyId == partyId);
        if (branchId.HasValue) baseQ = baseQ.Where(l => l.Voucher!.BranchId == branchId);

        // Opening = signed net before the window.
        var before = await baseQ.Where(l => l.Voucher!.VoucherDate < from.Date)
            .Select(l => new { l.DrCr, l.Amount }).ToListAsync();
        decimal openingSigned = before.Sum(x => Signed(x.Amount, x.DrCr));

        var lines = await baseQ
            .Where(l => l.Voucher!.VoucherDate >= from.Date && l.Voucher!.VoucherDate <= to.Date)
            .Select(l => new { l.Voucher!.VoucherDate, l.Voucher!.VoucherNo, l.Voucher!.Type,
                               l.Voucher!.ReferenceNo, l.Narration, l.DrCr, l.Amount })
            .OrderBy(l => l.VoucherDate).ThenBy(l => l.VoucherNo)
            .ToListAsync();

        // Present in party-normal sign: A/R debit-normal (+ = they owe us); A/P credit-normal (+ = we owe).
        decimal Norm(decimal signed) => creditNormal ? -signed : signed;

        report.OpeningBalance = Norm(openingSigned);
        decimal running = openingSigned;
        foreach (var l in lines)
        {
            decimal dr = l.DrCr == DrCr.Debit  ? l.Amount : 0;
            decimal cr = l.DrCr == DrCr.Credit ? l.Amount : 0;
            running += dr - cr;
            report.TotalDebit  += dr;
            report.TotalCredit += cr;
            report.Entries.Add(new StatementEntry
            {
                Date      = l.VoucherDate,
                VoucherNo = l.VoucherNo,
                DocType   = l.Type switch { VoucherType.Journal => "Bill", VoucherType.Receipt => "Receipt",
                                            VoucherType.Payment => "Payment", _ => "Journal" },
                Reference = l.ReferenceNo,
                Narration = l.Narration,
                Debit     = dr,
                Credit    = cr,
                RunningBalance = Norm(running),
            });
        }

        report.ClosingBalance = Norm(running);
        report.Outstanding    = report.ClosingBalance > 0 ? report.ClosingBalance : 0;
        return report;
    }

    // ── 7. Receivable / Payable aging (FIFO open-item) ────────────────────────
    public Task<List<AgingRow>> GetReceivableAgingAsync(DateTime asOf, int? branchId = null) =>
        AgingAsync(AccountSeed.Codes.AccountsReceivable, invoiceSide: DrCr.Debit, asOf, branchId);

    public Task<List<AgingRow>> GetPayableAgingAsync(DateTime asOf, int? branchId = null) =>
        AgingAsync(AccountSeed.Codes.AccountsPayable, invoiceSide: DrCr.Credit, asOf, branchId);

    private async Task<List<AgingRow>> AgingAsync(string headCode, DrCr invoiceSide, DateTime asOf, int? branchId)
    {
        var head = await HeadByCodeAsync(headCode);
        if (head is null) return new();

        var q = _db.VoucherLines.AsNoTracking()
            .Where(l => l.AccountHeadId == head.Id
                     && l.Voucher!.Status == VoucherStatus.Posted
                     && l.Voucher!.VoucherDate <= asOf.Date
                     && l.Voucher!.PartyId != null);
        if (branchId.HasValue) q = q.Where(l => l.Voucher!.BranchId == branchId);

        var raw = await q.Select(l => new { PartyId = l.Voucher!.PartyId!.Value, l.Voucher!.VoucherDate, l.DrCr, l.Amount })
            .ToListAsync();

        var names = await _db.Clients.AsNoTracking()
            .Where(c => raw.Select(r => r.PartyId).Distinct().Contains(c.Id))
            .Select(c => new { c.Id, c.CompanyName }).ToListAsync();
        var nameMap = names.ToDictionary(x => x.Id, x => x.CompanyName);

        var rows = new List<AgingRow>();
        foreach (var grp in raw.GroupBy(r => r.PartyId))
        {
            // Open invoices (invoiceSide) oldest first; settlements (opposite side) applied FIFO.
            var invoices = grp.Where(x => x.DrCr == invoiceSide)
                .OrderBy(x => x.VoucherDate)
                .Select(x => new { x.VoucherDate, Open = x.Amount }).ToList();
            decimal settle = grp.Where(x => x.DrCr != invoiceSide).Sum(x => x.Amount);

            // Apply settlements oldest-first; whatever remains open is aged by the invoice date.
            var applied = new List<(DateTime Date, decimal Amt)>();
            foreach (var inv in invoices)
            {
                decimal amt = inv.Open;
                if (settle > 0)
                {
                    var used = Math.Min(settle, amt);
                    amt -= used; settle -= used;
                }
                if (amt > 0) applied.Add((inv.VoucherDate, amt));
            }

            var row = new AgingRow
            {
                PartyId = grp.Key,
                PartyName = nameMap.TryGetValue(grp.Key, out var n) ? n : $"#{grp.Key}",
            };
            foreach (var (date, amt) in applied)
            {
                var days = (asOf.Date - date.Date).Days;
                if      (days <= 30) row.Current    += amt;
                else if (days <= 60) row.Days31_60  += amt;
                else if (days <= 90) row.Days61_90  += amt;
                else                 row.Days90Plus += amt;
            }
            row.Total = row.Current + row.Days31_60 + row.Days61_90 + row.Days90Plus;
            if (row.Total != 0) rows.Add(row);
        }

        return rows.OrderByDescending(r => r.Total).ToList();
    }

    // ── 8. Job Profitability (revenue from bills − cost from operations) ──────
    public async Task<List<JobProfitabilityRow>> GetJobProfitabilityAsync(
        DateTime from, DateTime to, int? branchId = null, int? customerId = null)
    {
        var jq = _db.JobOrders.AsNoTracking().Include(j => j.BillingClient)
            .Where(j => (j.Mode == JobMode.Clearance || j.Mode == JobMode.Forwarding)
                     && (j.Status == JobOrderStatus.Approved || j.Status == JobOrderStatus.Closed)
                     && j.JobOrderDate >= from.Date && j.JobOrderDate <= to.Date);
        if (branchId.HasValue)  jq = jq.Where(j => j.BranchId == branchId);
        if (customerId.HasValue) jq = jq.Where(j => j.BillingClientId == customerId);

        var jobs = await jq.OrderByDescending(j => j.Id).ToListAsync();
        var jobIds = jobs.Select(j => j.Id).ToList();

        var rev = (await _db.Bills.AsNoTracking()
            .Where(b => b.JobOrderId != null && jobIds.Contains(b.JobOrderId.Value)
                     && (b.Status == BillStatus.Approved || b.Status == BillStatus.Closed))
            .GroupBy(b => b.JobOrderId!.Value)
            .Select(g => new { JobId = g.Key, Rev = g.Sum(x => x.SubTotal) })
            .ToListAsync()).ToDictionary(x => x.JobId, x => x.Rev);

        var ops = await _db.JobOrderOperations.AsNoTracking()
            .Where(o => jobIds.Contains(o.JobOrderId) && o.Cost != null && o.Cost > 0)
            .Select(o => new { o.JobOrderId, o.ExpenseCategory, Cost = o.Cost!.Value })
            .ToListAsync();

        var rows = new List<JobProfitabilityRow>(jobs.Count);
        foreach (var j in jobs)
        {
            var jOps = ops.Where(o => o.JobOrderId == j.Id).ToList();
            decimal trans = jOps.Where(o => o.ExpenseCategory == ChargeCategory.Transport).Sum(o => o.Cost);
            decimal clear = jOps.Where(o => o.ExpenseCategory is ChargeCategory.Customs or ChargeCategory.Port
                                        or ChargeCategory.Labour or ChargeCategory.Documentation).Sum(o => o.Cost);
            decimal other = jOps.Where(o => o.ExpenseCategory is not (ChargeCategory.Transport or ChargeCategory.Customs
                                        or ChargeCategory.Port or ChargeCategory.Labour or ChargeCategory.Documentation)).Sum(o => o.Cost);
            decimal revenue = rev.TryGetValue(j.Id, out var r) ? r : 0;
            decimal totalCost = trans + clear + other;
            decimal profit = revenue - totalCost;

            rows.Add(new JobProfitabilityRow
            {
                JobOrderId         = j.Id,
                JobOrderNo         = j.JobOrderNo,
                Customer           = j.BillingClient?.CompanyName,
                JobDate            = j.JobOrderDate,
                Revenue            = revenue,
                TransportationCost = trans,
                ClearanceCost      = clear,
                OtherCost          = other,
                TotalCost          = totalCost,
                Profit             = profit,
                ProfitPct          = revenue != 0 ? decimal.Round(profit * 100m / revenue, 2) : 0,
            });
        }
        return rows;
    }

    // ── 9. Mode registers (thin wrappers over the bill register) ─────────────
    public Task<List<BillRegisterRow>> GetTransportationReportAsync(DateTime from, DateTime to, BillStatus? status = null, int? branchId = null) =>
        GetBillRegisterAsync(from, to, BillMode.Transportation, status, branchId);

    public Task<List<BillRegisterRow>> GetForwardingReportAsync(DateTime from, DateTime to, BillStatus? status = null, int? branchId = null) =>
        GetBillRegisterAsync(from, to, BillMode.Forwarding, status, branchId);

    public Task<List<BillRegisterRow>> GetClearanceReportAsync(DateTime from, DateTime to, BillStatus? status = null, int? branchId = null) =>
        GetBillRegisterAsync(from, to, BillMode.Clearance, status, branchId);

    // ── 10. Dashboard KPI snapshot ────────────────────────────────────────────
    public async Task<DashboardKpis> GetDashboardKpisAsync(DateTime from, DateTime to, int? branchId = null)
    {
        var k = new DashboardKpis { FromDate = from.Date, ToDate = to.Date };

        var jq = _db.JobOrders.AsNoTracking()
            .Where(j => (j.Mode == JobMode.Clearance || j.Mode == JobMode.Forwarding));
        if (branchId.HasValue) jq = jq.Where(j => j.BranchId == branchId);
        k.JobsDraft    = await jq.CountAsync(j => j.Status == JobOrderStatus.Draft);
        k.JobsApproved = await jq.CountAsync(j => j.Status == JobOrderStatus.Approved);
        k.JobsClosed   = await jq.CountAsync(j => j.Status == JobOrderStatus.Closed);

        var bq = _db.Bills.AsNoTracking().AsQueryable();
        if (branchId.HasValue) bq = bq.Where(b => b.BranchId == branchId);
        k.BillsDraft    = await bq.CountAsync(b => b.Status == BillStatus.Draft);
        k.BillsApproved = await bq.CountAsync(b => b.Status == BillStatus.Approved);

        var pl = await GetProfitAndLossAsync(from, to, branchId);
        k.TotalRevenue = pl.TotalRevenue;
        k.TotalExpense = pl.TotalDirectCost + pl.TotalIndirectCost;
        k.NetProfit    = pl.NetProfit;

        k.Receivables     = await GroupBalanceAsync(AccountSeed.Codes.AccountsReceivable, branchId);
        k.Payables        = -await GroupBalanceAsync(AccountSeed.Codes.AccountsPayable, branchId);   // credit-normal
        k.CashBankBalance = await CashBankBalanceAsync(branchId);
        return k;
    }

    private async Task<decimal> GroupBalanceAsync(string code, int? branchId)
    {
        var head = await HeadByCodeAsync(code);
        if (head is null) return 0;
        var agg = await PostedLines(branchId)
            .Where(l => l.AccountHeadId == head.Id)
            .Select(l => new { l.DrCr, l.Amount }).ToListAsync();
        return Signed(head.OpeningBalance, head.OpeningBalanceType) + agg.Sum(x => Signed(x.Amount, x.DrCr));
    }

    private async Task<decimal> CashBankBalanceAsync(int? branchId)
    {
        var heads = await _db.AccountHeads.AsNoTracking().Where(a => a.IsCash || a.IsBank).ToListAsync();
        decimal total = 0;
        foreach (var h in heads)
        {
            var agg = await PostedLines(branchId).Where(l => l.AccountHeadId == h.Id)
                .Select(l => new { l.DrCr, l.Amount }).ToListAsync();
            total += Signed(h.OpeningBalance, h.OpeningBalanceType) + agg.Sum(x => Signed(x.Amount, x.DrCr));
        }
        return total;
    }
}
