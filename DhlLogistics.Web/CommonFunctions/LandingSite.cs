namespace DhlLogistics.Web.CommonFunctions;

/// <summary>
/// The public marketing site that ships as plain HTML/CSS/JS under <c>wwwroot/landing/</c>.
///
/// <para>Visitors see clean, extensionless URLs — <c>/Pvgt</c>, <c>/Agency</c>, … — not the
/// physical <c>/landing/pvgt.html</c>. That is a path REWRITE, not a redirect: the middleware
/// swaps <see cref="HttpRequest.Path"/> before UseStaticFiles reads it, so the file is served
/// while the address bar keeps the clean URL. Because the browser's base URL is then <c>/Pvgt</c>
/// rather than <c>/landing/</c>, every asset reference inside those HTML files is absolute
/// (<c>/landing/pvgt.css</c>) and every page link uses the clean URL.</para>
///
/// <para>None of these names collide with a Blazor <c>@page</c> route — the app's clearing /
/// forwarding / customs screens all live under <c>/jobs/</c>, <c>/bills/</c> or <c>/reports/</c>.
/// Check <see cref="CleanUrlToFile"/> against the route table before adding a page here, since a
/// rewrite runs ahead of routing and would silently shadow an app route.</para>
/// </summary>
public static class LandingSite
{
    /// <summary>Folder the static assets (css/js/images) are served from.</summary>
    public const string AssetRoot = "/landing";

    /// <summary>Entry page — where anonymous visitors and signed-out users go.</summary>
    public const string Home = "/Pvgt";

    /// <summary>
    /// Clean URL → physical file under wwwroot. Ordinal-ignore-case so /pvgt, /PVGT and /Pvgt
    /// all work; <see cref="Home"/> and the links inside the pages define the canonical casing.
    /// </summary>
    private static readonly Dictionary<string, string> CleanUrlToFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Pvgt"]          = "/landing/pvgt.html",
        ["/Agency"]        = "/landing/agency.html",
        ["/Certification"] = "/landing/certification.html",
        ["/Clearing"]      = "/landing/clearing.html",
        ["/Customs"]       = "/landing/customs.html",
        ["/Forwarding"]    = "/landing/forwarding.html",
        ["/Legacy"]        = "/landing/legacy.html",
        ["/Privacy"]       = "/landing/privacy.html",
        ["/Terms"]         = "/landing/terms.html",
    };

    /// <summary>Reverse map, so a request for the raw .html can be redirected to its clean URL.</summary>
    private static readonly Dictionary<string, string> FileToCleanUrl =
        CleanUrlToFile.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> is a clean landing URL; yields the file to serve.</summary>
    public static bool TryGetFile(PathString path, out string file)
    {
        // Tolerate a trailing slash ("/Pvgt/") — browsers and pasted links produce both forms.
        var key = path.Value?.TrimEnd('/');
        if (string.IsNullOrEmpty(key)) { file = ""; return false; }
        return CleanUrlToFile.TryGetValue(key, out file!);
    }

    /// <summary>True when <paramref name="path"/> is a raw landing .html; yields its canonical clean URL.</summary>
    public static bool TryGetCleanUrl(PathString path, out string cleanUrl)
    {
        var key = path.Value;
        if (string.IsNullOrEmpty(key)) { cleanUrl = ""; return false; }
        return FileToCleanUrl.TryGetValue(key, out cleanUrl!);
    }
}
