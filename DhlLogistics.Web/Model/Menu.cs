namespace DhlLogistics.Web.Model;

/// <summary>
/// A navigation menu entry. Ported from CBM's <c>Menu</c> table, adapted to the
/// DHL free-hand sidebar: <see cref="Icon"/> holds the emoji glyph rendered in the
/// sidebar and <see cref="PageName"/> holds the relative href (e.g. "masters/clients")
/// which doubles as the permission page-path used to filter the menu per user.
///
/// Lives in a SEPARATE local SQL Server database (see <see cref="Database.MenuDbContext"/>),
/// independent of the app's Postgres store.
/// </summary>
public class Menu
{
    public int MenuId { get; set; }

    /// <summary>Display text in the sidebar.</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>Null for a top-level item; otherwise the parent group's MenuId.</summary>
    public int? ParentId { get; set; }

    /// <summary>Soft on/off switch — inactive rows are never rendered.</summary>
    public bool Active { get; set; } = true;

    /// <summary>Emoji glyph shown before the label.</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Relative href and permission page-path. Null/empty for a pure group header
    /// (a collapsible parent that navigates nowhere). The dashboard uses "".
    /// </summary>
    public string? PageName { get; set; }

    /// <summary>Sort order within the same parent.</summary>
    public int ShowOrder { get; set; }

    /// <summary>The home/dashboard link — always visible, matches the root route.</summary>
    public bool IsDashboard { get; set; }

    /// <summary>Render the NavLink with <c>NavLinkMatch.All</c> (exact-path active state).</summary>
    public bool MatchAll { get; set; }

    /// <summary>A top-level group whose &lt;details&gt; starts expanded.</summary>
    public bool DefaultOpen { get; set; }

    /// <summary>
    /// When false the item bypasses permission filtering and is shown to every
    /// authenticated user (e.g. Dashboard, Staff). When true the user must hold a
    /// View grant on <see cref="PageName"/>.
    /// </summary>
    public bool RequiresPermission { get; set; } = true;
}
