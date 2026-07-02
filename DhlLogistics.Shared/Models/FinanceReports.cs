namespace DhlLogistics.Shared.Models;

// ── Ledger ───────────────────────────────────────────────────────────────────

/// <summary>One Dr/Cr line that hit a specific account in a date window.</summary>
public class LedgerEntry
{
    public DateTime Date { get; set; }
    public string   VoucherNo   { get; set; } = string.Empty;
    public VoucherType VoucherType { get; set; }
    public string?  Narration   { get; set; }
    public string?  Reference   { get; set; }
    public string?  ContraAccount { get; set; }   // for a 2-line voucher: the other side
    public decimal  Debit       { get; set; }
    public decimal  Credit      { get; set; }

    /// <summary>Running balance after this entry. Sign convention: + = Dr balance, − = Cr balance.</summary>
    public decimal  RunningBalance { get; set; }
}

public class LedgerReport
{
    public int      AccountHeadId   { get; set; }
    public string   AccountCode     { get; set; } = string.Empty;
    public string   AccountName     { get; set; } = string.Empty;
    public AccountGroup Group       { get; set; }

    public DateTime FromDate        { get; set; }
    public DateTime ToDate          { get; set; }

    public decimal  OpeningBalance       { get; set; }   // signed: + Dr, − Cr
    public decimal  PeriodDebit          { get; set; }
    public decimal  PeriodCredit         { get; set; }
    public decimal  ClosingBalance       { get; set; }   // signed

    public List<LedgerEntry> Entries { get; set; } = new();
}

// ── Trial Balance ────────────────────────────────────────────────────────────

public class TrialBalanceRow
{
    public int    AccountHeadId { get; set; }
    public string AccountCode   { get; set; } = string.Empty;
    public string AccountName   { get; set; } = string.Empty;
    public AccountGroup Group   { get; set; }

    public decimal OpeningDebit  { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit   { get; set; }
    public decimal PeriodCredit  { get; set; }
    public decimal ClosingDebit  { get; set; }
    public decimal ClosingCredit { get; set; }
}

// ── GST Output Register ──────────────────────────────────────────────────────

public class GstOutputRow
{
    public long     BillId       { get; set; }
    public string   BillNo       { get; set; } = string.Empty;
    public DateTime BillDate     { get; set; }
    public BillMode Mode         { get; set; }
    public string?  ClientName   { get; set; }
    public string?  ClientGstin  { get; set; }    // reserved — DhlClient may not have it yet
    public string?  Branch       { get; set; }

    public decimal  TaxableValue { get; set; }    // Bill.SubTotal
    public decimal  GstAmount    { get; set; }    // Bill.GstAmount
    public decimal  GstRate      { get; set; }    // weighted average from charges
    public decimal  TotalAmount  { get; set; }
    public BillStatus Status     { get; set; }
}

// ── Combined Bill Register ───────────────────────────────────────────────────

public class BillRegisterRow
{
    public long     BillId       { get; set; }
    public string   BillNo       { get; set; } = string.Empty;
    public DateTime BillDate     { get; set; }
    public BillMode Mode         { get; set; }
    public string?  ClientName   { get; set; }
    public string?  Branch       { get; set; }
    public string?  JobOrderNo   { get; set; }
    public string?  Currency     { get; set; }
    public decimal  ExchangeRate { get; set; }
    public decimal  SubTotal     { get; set; }
    public decimal  GstAmount    { get; set; }
    public decimal  TotalAmount  { get; set; }
    public BillStatus Status     { get; set; }
    public string?  CreatedBy    { get; set; }
    public string?  ApprovedBy   { get; set; }
}

// ── Reporting granularity ─────────────────────────────────────────────────────
public enum ReportGranularity { Daily = 1, Monthly = 2, Yearly = 3 }

// ── Profit & Loss ─────────────────────────────────────────────────────────────
public class PLLine
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ProfitAndLossReport
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate   { get; set; }
    public List<PLLine> Revenue       { get; set; } = new();
    public List<PLLine> DirectCosts   { get; set; } = new();
    public List<PLLine> IndirectCosts { get; set; } = new();
    public decimal TotalRevenue      { get; set; }
    public decimal TotalDirectCost   { get; set; }
    public decimal TotalIndirectCost { get; set; }
    public decimal GrossProfit       { get; set; }   // Revenue − direct costs
    public decimal NetProfit         { get; set; }   // Gross − indirect costs
}

// ── Job Profitability ─────────────────────────────────────────────────────────
public class JobProfitabilityRow
{
    public long     JobOrderId         { get; set; }
    public string   JobOrderNo         { get; set; } = string.Empty;
    public string?  Customer           { get; set; }
    public DateTime JobDate            { get; set; }
    public decimal  Revenue            { get; set; }
    public decimal  TransportationCost { get; set; }
    public decimal  ClearanceCost      { get; set; }
    public decimal  OtherCost          { get; set; }
    public decimal  TotalCost          { get; set; }
    public decimal  Profit             { get; set; }
    public decimal  ProfitPct          { get; set; }
}

// ── Customer / Vendor Statement ───────────────────────────────────────────────
public class StatementEntry
{
    public DateTime Date        { get; set; }
    public string   VoucherNo   { get; set; } = string.Empty;
    public string   DocType     { get; set; } = string.Empty;   // Bill / Receipt / Payment / Journal
    public string?  Reference   { get; set; }
    public string?  Narration   { get; set; }
    public decimal  Debit       { get; set; }
    public decimal  Credit      { get; set; }
    public decimal  RunningBalance { get; set; }
}

public class PartyStatementReport
{
    public int      PartyId        { get; set; }
    public string?  PartyName      { get; set; }
    public DateTime FromDate       { get; set; }
    public DateTime ToDate         { get; set; }
    public decimal  OpeningBalance { get; set; }
    public decimal  TotalDebit     { get; set; }
    public decimal  TotalCredit    { get; set; }
    public decimal  ClosingBalance { get; set; }
    public decimal  Outstanding    { get; set; }   // amount still owed (party-normal sign)
    public List<StatementEntry> Entries { get; set; } = new();
}

// ── Receivable / Payable Aging ────────────────────────────────────────────────
public class AgingRow
{
    public int     PartyId    { get; set; }
    public string? PartyName  { get; set; }
    public decimal Current    { get; set; }   // 0–30 days
    public decimal Days31_60  { get; set; }
    public decimal Days61_90  { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal Total      { get; set; }
}

// ── Revenue / Expense by period ───────────────────────────────────────────────
public class PeriodAmountRow
{
    public string   Period      { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public decimal  Amount      { get; set; }
}

// ── Payment Register ──────────────────────────────────────────────────────────
public class PaymentRegisterRow
{
    public long        VoucherId { get; set; }
    public DateTime    Date      { get; set; }
    public string      VoucherNo { get; set; } = string.Empty;
    public VoucherType Type      { get; set; }
    public string?     Party     { get; set; }
    public string?     CashBank  { get; set; }
    public decimal     Amount    { get; set; }
    public string?     Reference { get; set; }
    public string?     Narration { get; set; }
}

// ── Balance Sheet ─────────────────────────────────────────────────────────────
public class BalanceSheetLine
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class BalanceSheetReport
{
    public DateTime AsOfDate { get; set; }
    public List<BalanceSheetLine> Assets      { get; set; } = new();
    public List<BalanceSheetLine> Liabilities { get; set; } = new();
    public List<BalanceSheetLine> Equity      { get; set; } = new();
    public decimal TotalAssets      { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity      { get; set; }
    public decimal NetProfit        { get; set; }
}

// ── Dashboard KPI ─────────────────────────────────────────────────────────────
public class DashboardKpis
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate   { get; set; }
    public int JobsDraft    { get; set; }
    public int JobsApproved { get; set; }
    public int JobsClosed   { get; set; }
    public int BillsDraft    { get; set; }
    public int BillsApproved { get; set; }
    public decimal TotalRevenue    { get; set; }
    public decimal TotalExpense    { get; set; }
    public decimal NetProfit       { get; set; }
    public decimal Receivables     { get; set; }
    public decimal Payables        { get; set; }
    public decimal CashBankBalance { get; set; }
}
