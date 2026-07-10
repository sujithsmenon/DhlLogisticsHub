namespace DhlLogistics.Web.Service.Search;

using System.Diagnostics;
using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

/// <summary>
/// The universal search orchestrator. Discovers every registered <see cref="ISearchProvider"/>, parses the
/// query for a smart-search keyword, filters providers by the user's View permissions, runs each provider's
/// server-side query against ONE short-lived context, groups + caps the results, and writes an audit row.
/// A new module becomes searchable simply by registering another provider — this class never changes.
/// </summary>
public class GlobalSearchService
{
    private readonly IEnumerable<ISearchProvider> _providers;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PermissionService _perms;
    private readonly AuthenticationStateProvider _auth;
    private readonly ILogger<GlobalSearchService> _log;

    public GlobalSearchService(IEnumerable<ISearchProvider> providers,
                               IDbContextFactory<AppDbContext> factory,
                               PermissionService perms,
                               AuthenticationStateProvider auth,
                               ILogger<GlobalSearchService> log)
    {
        _providers = providers;
        _factory   = factory;
        _perms     = perms;
        _auth      = auth;
        _log       = log;
    }

    public const int PerModuleLimit = 10;
    public const int TotalLimit     = 100;

    /// <summary>Registered modules — for a "search everything" legend / future settings screen.</summary>
    public IEnumerable<(string Module, string Icon)> Modules =>
        _providers.Select(p => (p.Module, p.Icon));

    public async Task<SearchResponse> SearchAsync(string term, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var q  = Parse(term);
        if (q.NormalizedText.Length < 2 && q.ModuleHint is null)
            return new SearchResponse(Array.Empty<SearchGroup>(), 0, 0, null);

        var user     = (await _auth.GetAuthenticationStateAsync()).User;
        var viewable = await _perms.GetViewablePagePathsAsync(user);   // normalised page-paths the user may View

        // Providers the user is allowed to see. A hinted module is run first so its group leads the list.
        var allowed = _providers.Where(p => IsAllowed(p, viewable)).ToList();
        var ordered = allowed
            .OrderByDescending(p => q.ModuleHint is not null && p.Keywords.Contains(q.ModuleHint, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var groups = new List<SearchGroup>();
        var total  = 0;

        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var p in ordered)
        {
            if (total >= TotalLimit) break;
            // With no free-text (e.g. bare "awb") only the hinted module runs — it lists its top rows.
            if (!q.HasText && !(q.ModuleHint is not null && p.Keywords.Contains(q.ModuleHint, StringComparer.OrdinalIgnoreCase)))
                continue;

            try
            {
                var take = Math.Min(PerModuleLimit, TotalLimit - total);
                var hits = await p.SearchAsync(db, q, take, ct);
                if (hits.Count > 0)
                {
                    groups.Add(new SearchGroup(p.Module, p.Icon, hits));
                    total += hits.Count;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Search provider {Module} failed for term '{Term}'.", p.Module, term);
            }
        }

        sw.Stop();
        _ = LogSearchAsync(user, q, groups.Sum(g => g.Hits.Count), (int)sw.ElapsedMilliseconds);
        return new SearchResponse(groups, total, (int)sw.ElapsedMilliseconds, q.ModuleHint);
    }

    /// <summary>Audit that a result was opened (best-effort, own context).</summary>
    public async Task LogOpenAsync(string term, string module, string primary)
    {
        try
        {
            var user = (await _auth.GetAuthenticationStateAsync()).User;
            await using var db = await _factory.CreateDbContextAsync();
            db.Set<SearchAuditLog>().Add(new SearchAuditLog
            {
                UserName      = user?.Identity?.Name,
                Term          = term,
                OpenedModule  = module,
                OpenedPrimary = primary,
                ResultCount   = 0,
                At            = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogDebug(ex, "Search open-audit skipped (non-fatal)."); }
    }

    // ── Query parsing (smart keyword + normalisation) ─────────────────────────
    private SearchQuery Parse(string raw)
    {
        raw = (raw ?? string.Empty).Trim();
        string? hint = null;
        var text = raw;

        var sp = raw.IndexOf(' ');
        var first = (sp > 0 ? raw[..sp] : raw).ToLowerInvariant();
        // First token is a module keyword?  ("invoice 250", "job 120", or a bare "awb").
        if (_providers.Any(p => p.Keywords.Contains(first, StringComparer.OrdinalIgnoreCase)))
        {
            hint = first;
            text = sp > 0 ? raw[(sp + 1)..].Trim() : string.Empty;
        }

        return new SearchQuery(raw, hint, text, SearchQuery.Strip(text));
    }

    private static bool IsAllowed(ISearchProvider p, HashSet<string>? viewable)
    {
        if (p.PermissionPaths.Length == 0) return true;   // unrestricted module
        if (viewable is null) return true;                // null ⇒ "all visible" (PermissionService contract)
        return p.PermissionPaths.Any(viewable.Contains);
    }

    private async Task LogSearchAsync(ClaimsPrincipal? user, SearchQuery q, int resultCount, int elapsedMs)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.Set<SearchAuditLog>().Add(new SearchAuditLog
            {
                UserName    = user?.Identity?.Name,
                Term        = q.Raw,
                ModuleHint  = q.ModuleHint,
                ResultCount = resultCount,
                ElapsedMs   = elapsedMs,
                At          = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogDebug(ex, "Search audit skipped (non-fatal)."); }
    }
}

/// <summary>Shared helpers for providers: separator-insensitive ranking + trimming to the module limit.
/// Providers fetch a small candidate set server-side, then rank in memory (exact ▸ starts-with ▸ contains).</summary>
public abstract class SearchProviderBase : ISearchProvider
{
    public abstract string   Module { get; }
    public abstract string   Icon { get; }
    public abstract string[] Keywords { get; }
    public abstract string[] PermissionPaths { get; }
    public abstract Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct);

    /// <summary>Candidate fetch cap — grab a few more than the display limit so in-memory ranking can float
    /// exact matches even when the DB ordering (recency/name) buried them. Public so shared query helpers
    /// (e.g. the bill helper) use the same cap.</summary>
    public const int FetchN = 40;
    protected const int Fetch = FetchN;

    protected static List<SearchHit> Rank(IEnumerable<SearchHit> hits, SearchQuery q, int take) =>
        hits.Select(h => h with { Rank = RankOf(h.Primary, h.Secondary, q) })
            .OrderBy(h => h.Rank)
            .ThenByDescending(h => h.Date ?? DateTime.MinValue)
            .Take(take)
            .ToList();

    private static int RankOf(string primary, string? secondary, SearchQuery q)
    {
        if (q.NormalizedText.Length == 0) return 2;
        var p = SearchQuery.Strip(primary);
        if (p == q.NormalizedText)              return 0;
        if (p.StartsWith(q.NormalizedText))     return 1;
        if (p.Contains(q.NormalizedText))       return 2;
        return 3;   // matched only via a secondary/related field
    }
}
