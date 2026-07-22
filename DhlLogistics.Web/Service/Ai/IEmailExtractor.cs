namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;

/// <summary>
/// The email content handed to an extractor. Attachments are passed as
/// metadata only (file names) in Phase 2 — their bytes are not parsed yet.
/// </summary>
public record EmailContent(
    string Subject,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<string> AttachmentNames);

/// <summary>
/// Provider-agnostic shipment extraction contract. Concrete providers
/// (OpenAI, Claude, Gemini, Azure OpenAI, a local heuristic …) implement this;
/// business code depends only on the interface. See <see cref="EmailAiReaderService"/>.
/// </summary>
public interface IEmailExtractor
{
    /// <summary>Stable provider key used for config-driven selection, e.g. "OpenAI".</summary>
    string Name { get; }

    /// <summary>
    /// Extract a shipment draft, or return <c>null</c> if this provider is
    /// unavailable (e.g. no API key) so the caller can fall back.
    /// </summary>
    Task<ShipmentDraft?> ExtractAsync(EmailContent email, CancellationToken ct = default);
}
