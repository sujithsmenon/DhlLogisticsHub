namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// AI Email Automation — Phase 2 entry point. Reads an email and returns a
/// <see cref="ShipmentDraft"/> for display. Provider-agnostic: it depends only
/// on <see cref="IEmailExtractor"/> and selects the primary provider from
/// configuration (<c>AiSettings:Provider</c>, default "OpenAI"). If the primary
/// provider is unavailable (no key) or throws, it automatically falls back to
/// the always-available "Heuristic" extractor and logs the reason.
///
/// Phase 2 performs NO database writes and creates NO shipment/job.
/// </summary>
public class EmailAiReaderService
{
    private readonly IReadOnlyList<IEmailExtractor> _extractors;
    private readonly ILogger<EmailAiReaderService> _log;
    private readonly string _providerName;

    public const string HeuristicProvider = "Heuristic";

    public EmailAiReaderService(
        IEnumerable<IEmailExtractor> extractors,
        IConfiguration config,
        ILogger<EmailAiReaderService> log)
    {
        _extractors = extractors.ToList();
        _log = log;
        _providerName = config["AiSettings:Provider"] is { Length: > 0 } p ? p : "OpenAI";
    }

    public async Task<ShipmentDraft> ReadAsync(EmailContent email, CancellationToken ct = default)
    {
        var heuristic = _extractors.First(e =>
            e.Name.Equals(HeuristicProvider, StringComparison.OrdinalIgnoreCase));

        var primary = _extractors.FirstOrDefault(e =>
            e.Name.Equals(_providerName, StringComparison.OrdinalIgnoreCase));

        // Primary is the heuristic (or not registered): no AI hop needed.
        if (primary is null || ReferenceEquals(primary, heuristic))
            return await heuristic.ExtractAsync(email, ct) ?? Empty();

        try
        {
            var draft = await primary.ExtractAsync(email, ct);
            if (draft is not null)
                return draft;

            _log.LogWarning("Provider '{Provider}' returned no draft; falling back to heuristic.", primary.Name);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Provider '{Provider}' failed; falling back to heuristic.", primary.Name);
        }

        var fallback = await heuristic.ExtractAsync(email, ct) ?? Empty();
        fallback.Notes.Insert(0, $"Primary provider '{primary.Name}' unavailable — used heuristic fallback.");
        return fallback;
    }

    private static ShipmentDraft Empty() =>
        new() { Provider = HeuristicProvider, Confidence = 0 };
}
