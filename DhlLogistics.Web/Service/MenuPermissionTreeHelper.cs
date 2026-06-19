namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Model;

/// <summary>
/// Pure helpers for the role/user permission tree, ported from CBM's
/// RolePermissions/UserPermissions cascade logic and generalised to all eight
/// <see cref="Permission"/> actions. Operates on an in-memory list of
/// <see cref="MenuPermissionTreeRow"/> built from menus (SQL Server) overlaid with
/// claim grants (Postgres).
/// </summary>
public static class MenuPermissionTreeHelper
{
    /// <summary>Build tree rows from menus, marking each action via <paramref name="granted"/>.</summary>
    public static List<MenuPermissionTreeRow> Build(IEnumerable<Menu> menus, Func<string, Permission, bool> granted)
    {
        var rows = new List<MenuPermissionTreeRow>();
        foreach (var m in menus)
        {
            var path = string.IsNullOrWhiteSpace(m.PageName) ? null : PermissionService.Normalise(m.PageName);
            var row = new MenuPermissionTreeRow
            {
                MenuId   = m.MenuId,
                ParentId = m.ParentId,
                MenuName = m.MenuName,
                PagePath = path,
                IsPage   = path is not null,
            };
            if (path is not null)
                foreach (var a in MenuPermissionTreeRow.Actions)
                    row.Set(a, granted(path, a));
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Apply a single checkbox change, enforcing the View dependency.</summary>
    public static void SetField(MenuPermissionTreeRow row, Permission field, bool value)
    {
        if (field == Permission.View)
        {
            row.View = value;
            if (!value) row.ClearAll();   // View off ⇒ nothing else allowed
            return;
        }

        if (!row.View) value = false;     // can't grant an action without View
        row.Set(field, value);
    }

    /// <summary>View turned off on a parent ⇒ clear every action on all descendants.</summary>
    public static List<MenuPermissionTreeRow> ClearChildren(List<MenuPermissionTreeRow> data, int parentId)
    {
        var affected = new List<MenuPermissionTreeRow>();
        foreach (var child in data.Where(x => x.ParentId == parentId))
        {
            child.ClearAll();
            affected.Add(child);
            affected.AddRange(ClearChildren(data, child.MenuId));
        }
        return affected;
    }

    /// <summary>Cascade a single action's value to all descendants.</summary>
    public static List<MenuPermissionTreeRow> CascadeChildren(List<MenuPermissionTreeRow> data, int parentId, Permission field, bool value)
    {
        var affected = new List<MenuPermissionTreeRow>();
        foreach (var child in data.Where(x => x.ParentId == parentId))
        {
            SetField(child, field, value);
            affected.Add(child);
            affected.AddRange(CascadeChildren(data, child.MenuId, field, value));
        }
        return affected;
    }

    /// <summary>Roll the parent chain's checkbox state up to reflect their children (UI only).</summary>
    public static void UpdateParent(List<MenuPermissionTreeRow> data, int? parentId, Permission field)
    {
        if (parentId is null) return;
        var parent = data.FirstOrDefault(x => x.MenuId == parentId.Value);
        if (parent is null) return;

        var siblings = data.Where(x => x.ParentId == parentId).ToList();
        bool allChecked = siblings.Count > 0 && siblings.All(x => x.Get(field));

        SetField(parent, field, allChecked);
        UpdateParent(data, parent.ParentId, field);
    }
}
