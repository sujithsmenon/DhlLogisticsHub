namespace DhlLogistics.Shared.Models;

/// <summary>
/// AI Email Automation — Phase 5. The SECOND approval gate: after a shipment is
/// created (Phase 4) from an approved draft, this record must be approved before
/// a Clearing (<see cref="JobMode.Clearance"/>) or Forwarding (<see cref="JobMode.Forwarding"/>)
/// JobOrder is created through the existing JobOrder workflow. Nothing bypasses it.
///
/// Links back to the <see cref="ShipmentDraftApproval"/> (and thus the email +
/// DHL Invoice Number master reference) and forward to the created JobOrder.
/// </summary>
public class ShipmentJobApproval
{
    public int Id { get; set; }

    // ── Back-links ──
    public int ShipmentDraftApprovalId { get; set; }
    public ShipmentDraftApproval? ShipmentDraftApproval { get; set; }

    /// <summary>"Awb" | "Export" — the Phase 4 shipment kind.</summary>
    public string ShipmentKind { get; set; } = string.Empty;
    public int ShipmentId { get; set; }

    // ── Denormalized context (display + client resolution) ──
    public string? DhlInvoiceNumber { get; set; }   // master business reference
    public string? CustomerName { get; set; }
    public string EmailSubject { get; set; } = string.Empty;

    // ── Proposed JobOrder shape (approver may change Mode at review) ──
    public JobMode         ProposedMode      { get; set; } = JobMode.Clearance;
    public JobShipmentMode ShipmentMode      { get; set; } = JobShipmentMode.Sea;
    public JobShipmentType ShipmentDirection { get; set; } = JobShipmentType.Import;

    // ── Review state ──
    public string Status { get; set; } = DraftApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }

    /// <summary>Id of the JobOrder created on approval; null until then (idempotency guard).</summary>
    public long? CreatedJobOrderId { get; set; }
}
