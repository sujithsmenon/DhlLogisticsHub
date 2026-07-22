namespace DhlLogistics.Shared.Models;

/// <summary>
/// AI Email Automation — Phase 3. A persisted, human-reviewable draft created
/// from an incoming email's AI extraction. It holds an editable snapshot of the
/// extracted shipment fields and awaits Approve / Reject. NOTHING downstream
/// (shipment, job, billing) is created until this is <see cref="DraftApprovalStatus.Approved"/>;
/// Phase 3 itself never creates a shipment.
///
/// Linked to <see cref="IncomingEmail"/> and carries the DHL Invoice Number,
/// the master business reference everything else links through.
/// </summary>
public class ShipmentDraftApproval
{
    public int Id { get; set; }

    public int IncomingEmailId { get; set; }
    public IncomingEmail? IncomingEmail { get; set; }

    /// <summary>Denormalized for the queue list (avoids a join per row).</summary>
    public string EmailSubject { get; set; } = string.Empty;

    // ── Editable extracted snapshot (approver may correct before approving) ──
    public string? ShipmentType { get; set; }     // "Air" | "Sea"
    public string? Direction { get; set; }         // "Import" | "Export"
    public string? Customer { get; set; }
    public string? DhlInvoiceNumber { get; set; }  // master business reference
    public string? ContainerNumber { get; set; }
    public string? Hawb { get; set; }
    public string? Mawb { get; set; }
    public string? BlNumber { get; set; }
    public string? OriginPort { get; set; }
    public string? DestinationPort { get; set; }
    public DateTime? Eta { get; set; }
    public DateTime? Etd { get; set; }
    public string? ReferenceNumbers { get; set; }

    // ── Extraction metadata ──
    public double Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
    public bool HighConfidence { get; set; }
    public string? ExtractionNotes { get; set; }

    // ── Review state ──
    public string Status { get; set; } = DraftApprovalStatus.Pending;

    // ── Phase 4: link to the shipment created from this approved draft ──
    /// <summary>"Awb" | "Export" once a shipment has been created; null until then.</summary>
    public string? CreatedShipmentType { get; set; }
    /// <summary>Id of the created AwbShipment / ExportJob; null until created (idempotency guard).</summary>
    public int? CreatedShipmentId { get; set; }
    public DateTime? ShipmentCreatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }   // reject reason / approver comment
}

/// <summary>Known values for <see cref="ShipmentDraftApproval.Status"/>.</summary>
public static class DraftApprovalStatus
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
