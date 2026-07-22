namespace DhlLogistics.Web.Service.Ai;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DhlLogistics.Shared.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// OpenAI Chat Completions extractor. Reads its API key and model from
/// configuration (<c>AiSettings:OpenAI:*</c>) — never hard-coded. Returns
/// <c>null</c> when no key is configured so <see cref="EmailAiReaderService"/>
/// falls back to the heuristic; throws on transport/parse errors, which the
/// service also treats as a fallback trigger.
/// </summary>
public class OpenAIEmailExtractor : IEmailExtractor
{
    public string Name => "OpenAI";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OpenAIEmailExtractor> _log;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    private const string SystemPrompt =
        "You extract shipment details from a freight-forwarding email. " +
        "Return ONLY a JSON object with exactly these keys: " +
        "shipmentType (\"Air\", \"Sea\", or null), direction (\"Import\", \"Export\", or null), " +
        "customer, dhlInvoiceNumber, containerNumber, hawb, mawb, blNumber, " +
        "originPort, destinationPort, eta (yyyy-MM-dd or null), etd (yyyy-MM-dd or null), " +
        "referenceNumbers, confidence (number 0-1). " +
        "Use null for any value not clearly present. Do not guess or invent values.";

    public OpenAIEmailExtractor(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<OpenAIEmailExtractor> log)
    {
        _httpFactory = httpFactory;
        _log = log;
        var s = config.GetSection("AiSettings:OpenAI");
        _apiKey  = s["ApiKey"] ?? string.Empty;
        _model   = string.IsNullOrWhiteSpace(s["Model"]) ? "gpt-4o-mini" : s["Model"]!;
        _baseUrl = string.IsNullOrWhiteSpace(s["BaseUrl"]) ? "https://api.openai.com/v1" : s["BaseUrl"]!;
    }

    public async Task<ShipmentDraft?> ExtractAsync(EmailContent email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _log.LogInformation("OpenAI extractor skipped: no API key configured (AiSettings:OpenAI:ApiKey).");
            return null; // -> caller falls back to heuristic
        }

        var userContent =
            $"Subject: {email.Subject}\n" +
            $"Attachments: {string.Join(", ", email.AttachmentNames)}\n\n" +
            $"Body:\n{email.TextBody ?? email.HtmlBody}";

        var request = new
        {
            model = _model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = userContent },
            },
        };

        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await http.PostAsJsonAsync($"{_baseUrl}/chat/completions", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"OpenAI returned {(int)resp.StatusCode}: {err}");
        }

        var payload = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        var json = payload?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("OpenAI response contained no content.");

        var parsed = JsonSerializer.Deserialize<ExtractionJson>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("OpenAI JSON could not be parsed.");

        return parsed.ToDraft();
    }

    // ── OpenAI response envelope (only the fields we use) ─────────────────────
    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        public sealed class Choice { [JsonPropertyName("message")] public Msg? Message { get; set; } }
        public sealed class Msg { [JsonPropertyName("content")] public string? Content { get; set; } }
    }

    // ── The model's JSON payload ──────────────────────────────────────────────
    private sealed class ExtractionJson
    {
        public string? ShipmentType { get; set; }
        public string? Direction { get; set; }
        public string? Customer { get; set; }
        public string? DhlInvoiceNumber { get; set; }
        public string? ContainerNumber { get; set; }
        public string? Hawb { get; set; }
        public string? Mawb { get; set; }
        public string? BlNumber { get; set; }
        public string? OriginPort { get; set; }
        public string? DestinationPort { get; set; }
        public string? Eta { get; set; }
        public string? Etd { get; set; }
        public string? ReferenceNumbers { get; set; }
        public double? Confidence { get; set; }

        public ShipmentDraft ToDraft() => new()
        {
            ShipmentType     = Norm(ShipmentType),
            Direction        = Norm(Direction),
            Customer         = Norm(Customer),
            DhlInvoiceNumber = Norm(DhlInvoiceNumber),
            ContainerNumber  = Norm(ContainerNumber),
            Hawb             = Norm(Hawb),
            Mawb             = Norm(Mawb),
            BlNumber         = Norm(BlNumber),
            OriginPort       = Norm(OriginPort),
            DestinationPort  = Norm(DestinationPort),
            Eta              = ParseDate(Eta),
            Etd              = ParseDate(Etd),
            ReferenceNumbers = Norm(ReferenceNumbers),
            Confidence       = Confidence is >= 0 and <= 1 ? Confidence.Value : 0.5,
            Provider         = "OpenAI",
        };

        private static string? Norm(string? s) =>
            string.IsNullOrWhiteSpace(s) || s.Equals("null", StringComparison.OrdinalIgnoreCase)
                ? null : s.Trim();

        private static DateTime? ParseDate(string? s) =>
            DateTime.TryParse(Norm(s), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : null;
    }
}
