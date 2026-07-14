namespace DhlLogistics.Shared.Models;

public enum BillMode
{
    Clearance      = 1,
    Forwarding     = 2,
    Transportation = 3,
}

/// <summary>
/// Which operational record a bill was raised from. Lets a single <see cref="Bill"/> (and the one
/// billing/approval/accounting workflow) serve every shipment type — JobOrders, AWB shipments and Export
/// jobs — without a separate billing module per type. Nullable/defaulted so existing bills (job-linked or
/// standalone) keep working unchanged.
/// </summary>
public enum BillSourceType
{
    JobOrder    = 0,   // Clearance / Forwarding job (also the implicit default for legacy job-linked bills)
    AwbShipment = 1,
    ExportJob   = 2,
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

/// <summary>Issuance state of a Bill *as an invoice*. Separate from the Bill
/// approval lifecycle (<see cref="BillStatus"/>) — a Bill is only issued as an
/// invoice after it is Approved. Room to add Sent / PartiallyPaid / Paid later.</summary>
public enum InvoiceStatus
{
    NotIssued = 0,
    Issued    = 10,
    Cancelled = 20,

    /// <summary>The bill's individual invoice was rolled into a consolidated <see cref="CustomerInvoice"/>.
    /// The original invoice row/PDF is NEVER deleted or rewritten — it stays for audit, flagged inactive, and
    /// <see cref="Bill.CustomerInvoiceId"/> points at the consolidated invoice that superseded it. There is
    /// exactly ONE active invoice per bill: the consolidated one.</summary>
    Superseded = 30,
}

public class Bill
{
    public long Id { get; set; }

    public BillMode Mode { get; set; } = BillMode.Clearance;

    /// <summary>Auto-generated, e.g. "CB/26-27/0001" / "FB/.." / "TB/..".</summary>
    public string BillNo { get; set; } = string.Empty;

    public DateTime BillDate { get; set; } = DateTime.UtcNow.Date;

    /// <summary>Customer's own invoice reference, copied from the source <see cref="JobOrder"/>. Purely
    /// operational — the legal tax-invoice number remains <see cref="BillNo"/> / <see cref="InvoiceNumber"/>.
    /// Jobs grouped into one billing invoice all share this value; printed on the invoice PDF beneath the
    /// primary Invoice No.</summary>
    public string? CustomerInvoiceNumber { get; set; }

    /// <summary>Indian FY starting year, e.g. 2026 = FY 2026-27.</summary>
    public int FinYear { get; set; }

    // ── Branch (optional) ────────────────────────────────────────────────────
    public int? BranchId { get; set; }
    public CompanyBranch? Branch { get; set; }

    // ── Source JobOrder (optional — bills can be standalone) ────────────────
    public long? JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    // ── Generic billing source (any shipment type) ──────────────────────────
    // For a job-linked bill: SourceType=JobOrder and SourceId=JobOrderId (kept in sync). For an AWB shipment
    // or Export job there is no JobOrder navigation, so the transport details below are snapshotted onto the
    // bill instead of read through a navigation. All nullable → existing bills are unaffected.
    public BillSourceType? SourceType { get; set; }
    public long?           SourceId   { get; set; }

    /// <summary>Human reference of the source record (Job No / HAWB No / Export job reference).</summary>
    public string? SourceReference { get; set; }
    /// <summary>Free-text shipment type descriptor for display (e.g. "AWB / Air", "Export Job", "Clearance / Sea").</summary>
    public string? ShipmentTypeName { get; set; }

    // ── Transport snapshot (populated from the source; "wherever available") ─
    public string? AwbOrBlNumber   { get; set; }
    public string? ContainerNumber { get; set; }
    public string? VehicleNumber   { get; set; }
    public string? DriverName       { get; set; }
    public string? Origin           { get; set; }
    public string? Destination      { get; set; }
    public string? PickupLocation   { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? CommodityName    { get; set; }
    public decimal? Quantity        { get; set; }
    public decimal? WeightKg        { get; set; }
    public decimal? VolumeCbm       { get; set; }

    public int?         TransporterId { get; set; }
    public Transporter? Transporter   { get; set; }

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

    // ── Invoice metadata (the Bill IS the ERP invoice — no separate entity) ──
    // Populated by InvoiceService.IssueInvoiceAsync once the Bill is Approved.
    // Reuses the Bill's own charges / GST / totals / accounting — nothing duplicated.
    public string?        InvoiceNumber  { get; set; }
    public DateTime?      InvoiceDate    { get; set; }
    public InvoiceStatus  InvoiceStatus  { get; set; } = InvoiceStatus.NotIssued;
    public bool           IsIssued       { get; set; }
    public DateTime?      IssueDate      { get; set; }
    public DateTime?      DueDate        { get; set; }
    public string?        PaymentTerms   { get; set; }
    public string?        InvoicePdfPath { get; set; }
    public string?        InvoiceRemarks { get; set; }

    // ── Consolidated customer invoice (Billing Group) ────────────────────────
    /// <summary>The consolidated <see cref="CustomerInvoice"/> this Bill was invoiced on, if any. Null for
    /// every existing bill and for any bill invoiced the old per-bill way — so legacy behaviour is unchanged.
    /// Non-null is also the <b>double-invoice guard</b>: a Bill on a consolidated invoice can neither be
    /// pulled onto a second one nor issued individually.</summary>
    public long?            CustomerInvoiceId { get; set; }
    public CustomerInvoice? CustomerInvoice   { get; set; }

    public List<BillCharge> Charges { get; set; } = new();
    public List<BillEvent>  Events  { get; set; } = new();

    /// <summary>Generated (customer-invoice PDF) and uploaded (vendor invoice /
    /// credit / debit note) documents attached to this Bill. One Bill → many docs.</summary>
    public List<InvoiceDocument> InvoiceDocuments { get; set; } = new();
}
