namespace DhlLogistics.Shared.Models;

public enum VoucherType
{
    Journal = 1,   // JV — generic
    Receipt = 2,   // money in
    Payment = 3,   // money out
    Contra  = 4,   // cash<->bank, bank<->bank
}

public enum VoucherStatus
{
    Draft     = 0,
    Submitted = 10,
    Verified  = 20,
    Approved  = 30,
    Rejected  = 40,
    Posted    = 50,   // accounting-period locked
}

public class Voucher
{
    public long Id { get; set; }

    public VoucherType Type { get; set; } = VoucherType.Journal;

    /// <summary>Auto-generated, e.g. "JV/26-27/0001", "RV/.." "PV/.." "CV/..".</summary>
    public string VoucherNo { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; } = DateTime.UtcNow.Date;
    public int      FinYear     { get; set; }

    public int? BranchId { get; set; }
    public CompanyBranch? Branch { get; set; }

    /// <summary>For Receipt/Payment — the Cash/Bank head money flows through.</summary>
    public int? CashOrBankAccountId { get; set; }
    public AccountHead? CashOrBankAccount { get; set; }

    /// <summary>Optional contra party (DhlClient) — printed on receipt/payment slip.</summary>
    public int? PartyId { get; set; }
    public DhlClient? Party { get; set; }

    public string? ReferenceNo { get; set; }
    public string  Narration   { get; set; } = string.Empty;

    public decimal TotalDebit  { get; set; }   // sum of Dr lines (must equal TotalCredit)
    public decimal TotalCredit { get; set; }   // sum of Cr lines

    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    // Audit trail (same pattern as JobOrder/Bill)
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
    public string?   ModifiedBy { get; set; }

    public DateTime? SubmittedOn { get; set; }
    public string?   SubmittedBy { get; set; }

    public DateTime? VerifiedOn { get; set; }
    public string?   VerifiedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }
    public string?   ApprovedBy { get; set; }

    public DateTime? RejectedOn       { get; set; }
    public string?   RejectedBy       { get; set; }
    public string?   RejectionReason  { get; set; }

    public DateTime? PostedOn { get; set; }
    public string?   PostedBy { get; set; }

    public List<VoucherLine>  Lines  { get; set; } = new();
    public List<VoucherEvent> Events { get; set; } = new();
}
