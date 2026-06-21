namespace DhlLogistics.Web.Model;

/// <summary>
/// One rendered sidebar row: a <see cref="Menu"/> plus its (already permission-filtered)
/// children. The sidebar is two levels deep — top-level groups containing leaf links, or
/// standalone top-level leaves (Dashboard, Staff) — so a node is a "group" exactly when it
/// has children. Shared by <c>Sidebar.razor</c> (builds the tree) and
/// <c>SidebarItem.razor</c> (renders one node).
/// </summary>
public sealed class MenuNode
{
    public required Menu Menu { get; init; }

    public List<MenuNode> Children { get; } = new();

    /// <summary>A collapsible parent (has children) vs. a direct link (leaf).</summary>
    public bool IsGroup => Children.Count > 0;
}
