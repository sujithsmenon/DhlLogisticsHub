namespace DhlLogistics.Shared.Models;

/// <summary>
/// Raw storage of an incoming DHL email, captured exactly as received.
/// Phase 1 of the AI Email Automation pipeline: storage only — no AI,
/// no workflow, no shipment/job creation. Later phases attach the AI
/// extraction, drafts, approvals and the DHL Invoice Number master
/// reference to this record.
/// </summary>
public class IncomingEmail
{
    public int Id { get; set; }

    // Identity / threading (from the MIME headers)
    public string MessageId { get; set; } = string.Empty;
    public string ThreadId  { get; set; } = string.Empty;

    // Envelope
    public string Subject { get; set; } = string.Empty;
    public string From    { get; set; } = string.Empty;
    public string To      { get; set; } = string.Empty;
    public string Cc      { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; }

    // Bodies, stored verbatim
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }

    /// <summary>Full original MIME source, preserved byte-for-byte.</summary>
    public byte[]? RawMime { get; set; }

    public bool HasAttachments { get; set; }

    /// <summary>
    /// Pipeline status. Phase 1 only ever sets <see cref="EmailProcessingStatus.Received"/>.
    /// </summary>
    public string ProcessingStatus { get; set; } = EmailProcessingStatus.Received;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<IncomingEmailAttachment> Attachments { get; set; } = new();
}

/// <summary>Known values for <see cref="IncomingEmail.ProcessingStatus"/>.</summary>
public static class EmailProcessingStatus
{
    public const string Received     = "Received";
    public const string DraftCreated = "DraftCreated";   // Phase 3: approval queued
    public const string Approved     = "Approved";       // Phase 3: draft approved
    public const string Rejected     = "Rejected";       // Phase 3: draft rejected
}
