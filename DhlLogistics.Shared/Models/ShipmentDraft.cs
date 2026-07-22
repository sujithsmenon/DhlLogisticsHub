namespace DhlLogistics.Shared.Models;

/// <summary>
/// AI Email Automation — Phase 2 output. A read-only, in-memory extraction
/// result produced from an incoming email. Phase 2 NEVER persists this or
/// creates a shipment/job; it is only returned for display and (later) for
/// building an approval draft.
/// </summary>
public class ShipmentDraft
{
    // "Air" | "Sea" | null
    public string? ShipmentType { get; set; }
    // "Import" | "Export" | null
    public string? Direction { get; set; }

    public string? Customer { get; set; }

    /// <summary>Master business reference — the DHL Invoice Number.</summary>
    public string? DhlInvoiceNumber { get; set; }

    public string? ContainerNumber { get; set; }
    public string? Hawb { get; set; }
    public string? Mawb { get; set; }
    public string? BlNumber { get; set; }

    public string? OriginPort { get; set; }
    public string? DestinationPort { get; set; }

    public DateTime? Eta { get; set; }
    public DateTime? Etd { get; set; }

    public string? ReferenceNumbers { get; set; }

    /// <summary>Overall extraction confidence, 0.0–1.0.</summary>
    public double Confidence { get; set; }

    /// <summary>Which extractor produced this draft ("OpenAI", "Heuristic", …).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Free-form notes/warnings (e.g. "fell back to heuristic").</summary>
    public List<string> Notes { get; set; } = new();
}
