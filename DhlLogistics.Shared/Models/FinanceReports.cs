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
