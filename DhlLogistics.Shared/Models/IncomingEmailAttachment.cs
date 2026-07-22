namespace DhlLogistics.Shared.Models;

/// <summary>
/// A single attachment belonging to an <see cref="IncomingEmail"/>, stored
/// with its raw bytes so the original email is preserved in full.
/// </summary>
public class IncomingEmailAttachment
{
    public int Id { get; set; }

    public int IncomingEmailId { get; set; }
    public IncomingEmail? IncomingEmail { get; set; }

    public string FileName    { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long   SizeBytes   { get; set; }

    /// <summary>Raw attachment content.</summary>
    public byte[]? Content { get; set; }
}
