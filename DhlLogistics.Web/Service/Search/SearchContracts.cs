namespace DhlLogistics.Web.Service.Search;

using DhlLogistics.Web.Database;

/// <summary>
/// Universal-search building blocks. Each searchable module implements <see cref="ISearchProvider"/> and is
/// registered in DI — the <see cref="GlobalSearchService"/> discovers all of them, so a NEW module only has
/// to add one provider class + a DI line, with no change to the search bar, the orchestrator or any other
/// module. Everything is server-side <c>IQueryable</c> against a short-lived context; nothing loads full
/// tables.
/// </summary>
public interface ISearchProvider
{
    /// <summary>Group label shown in the results ("Jobs", "Invoices", …).</summary>
    string Module { get; }

    /// <summary>Emoji/icon for the group + each row.</summary>
    string Icon { get; }

    /// <summary>Smart-search keywords that prioritise this module (e.g. "job", "invoice", "awb").
    /// Typing one as the first token floats this module to the top and searches the rest of the text.</summary>
    string[] Keywords { get; }

    /// <summary>Normalised page-paths this module lives under. The user sees results only when they may View
    /// at least one of these paths (reuses <see cref="PermissionService"/>). Empty ⇒ always allowed.</summary>
    string[] PermissionPaths { get; }

    /// <summary>Runs the module's server-side query and returns up to <paramref name="take"/> ranked hits.</summary>
    Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct);
}

/// <summary>A parsed query: the raw text, the module keyword (if the first token was one), the remaining
/// search text, and separator-stripped normalised forms used for fuzzy/spacing-insensitive matching.</summary>
public sealed record SearchQuery(string Raw, string? ModuleHint, string Text, string NormalizedText)
{
    public bool HasText => Text.Length > 0;

    /// <summary>%…% form for ILike CONTAINS on readable fields.</summary>
    public string Like => $"%{Text}%";

    /// <summary>%…% form for ILike CONTAINS on separator-stripped code/number fields.</summary>
    public string NormalizedLike => $"%{NormalizedText}%";

    /// <summary>lowercase + strip spaces / hyphens / slashes / dots — so "PVGT/00125", "PVGT-00125" and
    /// "PVGT 00125" all normalise to the same token.</summary>
    public static string Strip(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty
        : new string(s.Where(ch => ch is not (' ' or '-' or '/' or '.' or '\\')).ToArray()).ToLowerInvariant();
}

/// <summary>A single result row. Carries display data + navigation + quick actions (links to existing pages —
/// no new routes, no popups from the bar).</summary>
public sealed record SearchHit(
    string Module,
    string Icon,
    string Primary,
    string? Secondary,
    string? Status,
    string? Branch,
    DateTime? Date,
    string Url,
    IReadOnlyList<QuickAction> Actions)
{
    /// <summary>0 = exact, 1 = starts-with, 2 = contains, 3 = matched only on a secondary field. Lower first.</summary>
    public int Rank { get; init; } = 2;

    /// <summary>
    /// Related records reachable from this hit — the Billing Group chain (Customer Invoice → Bills → Jobs →
    /// Documents). Rendered under the row so the user sees the context without opening anything.
    /// Computed from data ALREADY fetched by the provider — never an extra per-row query.
    /// </summary>
    public string? Related { get; init; }

    /// <summary>Billing client / customer on this record. Drives the Advanced Search "Customer" filter.</summary>
    public string? Customer { get; init; }

    /// <summary>Sub-type within the module — the Bill mode (Clearance / Forwarding / Transportation) or the
    /// Job mode (Clearance / Forwarding). Drives the "Bill Type" and "Job Type" filters.</summary>
    public string? Type { get; init; }
}

/// <summary>
/// Advanced Search filters. Applied to the hits the providers return, so a provider needs no knowledge of
/// them — a module added later is filterable for free.
///
/// <para>Filters COMBINE with AND: every set filter must match. An unset filter matches everything.</para>
/// </summary>
public sealed class SearchFilter
{
    /// <summary>Restrict to these module names (empty = all modules).</summary>
    public HashSet<string> Modules { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string?   Status   { get; init; }
    public string?   Customer { get; init; }
    public string?   Branch   { get; init; }
    public DateTime? From     { get; init; }
    public DateTime? To       { get; init; }

    /// <summary>Clearance / Forwarding / Transportation — matched against <see cref="SearchHit.Type"/>.</summary>
    public string? BillType { get; init; }

    /// <summary>Clearance / Forwarding — matched against <see cref="SearchHit.Type"/>.</summary>
    public string? JobType { get; init; }

    public bool IsEmpty =>
        Modules.Count == 0 && Status is null && Customer is null && Branch is null
        && From is null && To is null && BillType is null && JobType is null;

    /// <summary>Does this hit satisfy every set filter?</summary>
    public bool Matches(SearchHit h)
    {
        if (Modules.Count > 0 && !Modules.Contains(h.Module)) return false;

        if (Status is not null &&
            !string.Equals(h.Status, Status, StringComparison.OrdinalIgnoreCase)) return false;

        if (Branch is not null &&
            !(h.Branch?.Contains(Branch, StringComparison.OrdinalIgnoreCase) ?? false)) return false;

        if (Customer is not null &&
            // fall back to Secondary: older providers put the client there
            !((h.Customer?.Contains(Customer, StringComparison.OrdinalIgnoreCase) ?? false)
              || (h.Secondary?.Contains(Customer, StringComparison.OrdinalIgnoreCase) ?? false))) return false;

        if (From is not null && (h.Date is null || h.Date.Value.Date < From.Value.Date)) return false;
        if (To   is not null && (h.Date is null || h.Date.Value.Date > To.Value.Date))   return false;

        // Bill Type / Job Type only constrain the modules they belong to — setting "Transportation" must not
        // wipe out the Clients group, it must simply not apply to it.
        if (BillType is not null && IsBillModule(h.Module) &&
            !string.Equals(h.Type, BillType, StringComparison.OrdinalIgnoreCase)) return false;

        if (JobType is not null && IsJobModule(h.Module) &&
            !string.Equals(h.Type, JobType, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private static bool IsBillModule(string module) =>
        module.Contains("Bill", StringComparison.OrdinalIgnoreCase)
        || module.Contains("Invoice", StringComparison.OrdinalIgnoreCase);

    private static bool IsJobModule(string module) =>
        module.Equals("Jobs", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A quick action on a result — a label, an icon and a destination route (an existing page).</summary>
public sealed record QuickAction(string Label, string Icon, string Url);

/// <summary>A module's results as one group, ready for the grouped dropdown.</summary>
public sealed record SearchGroup(string Module, string Icon, IReadOnlyList<SearchHit> Hits);

/// <summary>The full response: grouped hits + totals + timing (for the audit log / UI).</summary>
public sealed record SearchResponse(IReadOnlyList<SearchGroup> Groups, int Total, int ElapsedMs, string? ModuleHint);
