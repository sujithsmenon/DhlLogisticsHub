namespace DhlLogistics.Web.Service.Ai;

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DhlLogistics.Shared.Models;

/// <summary>
/// Regex/keyword extractor that needs no external service. Always available,
/// so it is the guaranteed fallback when the configured AI provider has no
/// key or fails. Weaker on unstructured prose than an LLM, but reliable and
/// free. Mirrors the pattern-matching style of <c>PdfParserService</c>.
/// </summary>
public class HeuristicEmailExtractor : IEmailExtractor
{
    public string Name => "Heuristic";

    // Labelled fields: "<label> : <value>"
    private static readonly Regex RxInvoice   = Label(@"(?:DHL\s+)?Invoice\s*(?:No|Number|#)?");
    private static readonly Regex RxHawb       = Label(@"HAWB(?:\s*No\.?)?");
    private static readonly Regex RxMawb       = Label(@"MAWB(?:\s*No\.?)?");
    private static readonly Regex RxBl         = Label(@"(?:B\s*/\s*L|BL|Bill\s*of\s*Lading)(?:\s*No\.?)?");
    private static readonly Regex RxCustomer   = Label(@"(?:Customer|Client|Consignee|Shipper)");
    private static readonly Regex RxReference  = Label(@"(?:Reference|Ref)\s*(?:No|Numbers?|#)?");
    private static readonly Regex RxOrigin     = Label(@"(?:Origin|Port\s*of\s*Loading|POL|From\s*Port)");
    private static readonly Regex RxDest       = Label(@"(?:Destination|Port\s*of\s*Discharge|POD|To\s*Port)");
    private static readonly Regex RxEta        = Label(@"ETA");
    private static readonly Regex RxEtd        = Label(@"ETD");

    // Free-form patterns
    private static readonly Regex RxContainer  = new(@"\b([A-Z]{4}\s?\d{7})\b", RegexOptions.Compiled);
    private static readonly Regex RxMawbNumber = new(@"\b(\d{3}-?\d{8})\b", RegexOptions.Compiled);
    private static readonly Regex RxTag        = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    // Block/cell boundaries become newlines so table rows don't collapse into
    // one line (which would let one labelled value swallow the next field).
    private static readonly Regex RxBlockBreak = new(
        @"</(?:td|tr|p|div|li|h[1-6]|table|thead|tbody)>|<br\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex Label(string label) =>
        new($@"{label}\s*[:\-]\s*([^\r\n<|]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<ShipmentDraft?> ExtractAsync(EmailContent email, CancellationToken ct = default)
    {
        var text = Compose(email);

        var draft = new ShipmentDraft { Provider = Name };

        draft.DhlInvoiceNumber = Field(RxInvoice, text);
        draft.Hawb             = Field(RxHawb, text);
        draft.Mawb             = Field(RxMawb, text) ?? Match(RxMawbNumber, text);
        draft.BlNumber         = Field(RxBl, text);
        draft.Customer         = Field(RxCustomer, text);
        draft.ReferenceNumbers = Field(RxReference, text);
        draft.OriginPort       = Field(RxOrigin, text);
        draft.DestinationPort  = Field(RxDest, text);
        draft.ContainerNumber  = Match(RxContainer, text);
        draft.Eta              = Date(Field(RxEta, text));
        draft.Etd              = Date(Field(RxEtd, text));

        draft.ShipmentType = InferShipmentType(text, draft);
        draft.Direction    = InferDirection(text);

        draft.Confidence = Score(draft);
        draft.Notes.Add("Extracted by local heuristic (no AI provider used).");

        return Task.FromResult<ShipmentDraft?>(draft);
    }

    private static string Compose(EmailContent email)
    {
        var body = !string.IsNullOrWhiteSpace(email.TextBody)
            ? email.TextBody!
            : StripHtml(email.HtmlBody ?? string.Empty);

        return string.Join('\n', new[]
        {
            email.Subject,
            body,
            string.Join(' ', email.AttachmentNames),
        });
    }

    private static string StripHtml(string html)
    {
        var withBreaks = RxBlockBreak.Replace(html, "\n");
        return WebUtility.HtmlDecode(RxTag.Replace(withBreaks, " "));
    }

    // ── Keyword inference ────────────────────────────────────────────────────
    private static string? InferShipmentType(string text, ShipmentDraft d)
    {
        var t = text.ToLowerInvariant();
        bool air = !string.IsNullOrEmpty(d.Hawb) || !string.IsNullOrEmpty(d.Mawb)
                   || Contains(t, "hawb", "mawb", "airway", "air waybill", "air freight", "flight", " awb");
        bool sea = !string.IsNullOrEmpty(d.BlNumber) || !string.IsNullOrEmpty(d.ContainerNumber)
                   || Contains(t, "ocean", "sea freight", "vessel", "container", "fcl", "lcl", "bill of lading");
        if (air && !sea) return "Air";
        if (sea && !air) return "Sea";
        return null; // ambiguous — leave for the human approver
    }

    private static string? InferDirection(string text)
    {
        var t = text.ToLowerInvariant();
        bool imp = Contains(t, "import", "inbound");
        bool exp = Contains(t, "export", "outbound");
        if (imp && !exp) return "Import";
        if (exp && !imp) return "Export";
        return null;
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var n in needles)
            if (haystack.Contains(n, StringComparison.Ordinal)) return true;
        return false;
    }

    // ── Field helpers ────────────────────────────────────────────────────────
    private static string? Field(Regex rx, string text)
    {
        var m = rx.Match(text);
        if (!m.Success) return null;
        var v = m.Groups[1].Value.Trim().Trim('|', ',', ';').Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? Match(Regex rx, string text)
    {
        var m = rx.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static readonly string[] DateFormats =
    {
        "dd-MMM-yyyy", "dd-MMM-yy", "dd/MM/yyyy", "dd/MM/yy",
        "yyyy-MM-dd", "d MMM yyyy", "dd MMM yyyy", "MM/dd/yyyy",
    };

    private static DateTime? Date(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
            return exact;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var any) ? any : null;
    }

    // Confidence = fraction of key business fields that were found.
    private static double Score(ShipmentDraft d)
    {
        var fields = new object?[]
        {
            d.ShipmentType, d.Direction, d.DhlInvoiceNumber, d.Customer,
            d.ContainerNumber, d.Hawb, d.Mawb, d.BlNumber,
            d.OriginPort, d.DestinationPort, d.Eta, d.Etd, d.ReferenceNumbers,
        };
        var found = fields.Count(f => f is string s ? !string.IsNullOrWhiteSpace(s) : f != null);
        // Cap heuristic confidence below a clean AI extraction to keep the
        // human-approval bar visible.
        return Math.Round(Math.Min(0.8, (double)found / fields.Length), 2);
    }
}
