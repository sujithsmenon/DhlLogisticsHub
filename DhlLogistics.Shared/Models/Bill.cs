namespace DhlLogistics.Shared.Models;

public enum BillMode
{
    Clearance      = 1,
    Forwarding     = 2,
    Transportation = 3,
}

public enum BillStatus
{
    Draft     = 0,
    Submitted = 10,
    Verified  = 20,
    Approved  = 30,
    Rejected  = 40,
    Closed    = 50,
}

public class Bill
{
    public long Id { get; set; }

    public BillMode Mode { get; set; } = BillMode.Clearance;

    /// <summary>Auto-generated, e.g. "CB/26-27/0001" / "FB/.." / "TB/..".</summary>
    public string BillNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; } = DateTime.UtcNow.Date;

    /// <summary>Indian FY starting year, e.g. 2026 = FY 2026-27.</summary>
    public int FinYear { get; set; }

    // ── Branch (optional) ────────────────────────────────────────────────────
    public int? BranchId { get; set; }
    public CompanyBranch? Branch { get; set; }

    // ── Source JobOrder (optional — bills can be standalone) ────────────────
    public long? JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    // ── Billing party (always a DhlClient) ───────────────────────────────────
    public int BillingClientId { get; set; }
    public DhlClient? BillingClient { get; set; }

    // ── Currency ─────────────────────────────────────────────────────────────
    public int? CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    public decimal ExchangeRate { get; set; } = 1m;

    // ── Computed totals (re-computed from BillCharges on save) ───────────────
    public decimal SubTotal     { get; set; }
    public decimal GstAmount    { get; set; }
    public decimal TotalAmount  { get; set; }

    // ── Status / lifecycle ───────────────────────────────────────────────────
    public BillStatus Status { get; set; } = BillStatus.Draft;

    public string? Reference { get; set; }
    public string? Remarks   { get; set; }

    // Audit trail (same pattern as JobOrder)
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

    public DateTime? ClosedOn { get; set; }
    public string?   ClosedBy { get; set; }

    public List<BillCharge> Charges { get; set; } = new();
    public List<BillEvent>  Events  { get; set; } = new();
}
