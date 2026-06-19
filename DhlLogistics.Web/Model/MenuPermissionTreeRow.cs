namespace DhlLogistics.Web.Model;

using DhlLogistics.Shared.Models;

/// <summary>
/// A row in the role/user permission tree (ported from CBM's MenuPermissionTreeRow).
/// One row per <see cref="Menu"/>. Group headers (no <see cref="PagePath"/>) are shown
/// for hierarchy and cascade UI only; permissions are persisted as claims keyed by
/// <see cref="PagePath"/>, so only rows with <see cref="IsPage"/> true are saved.
/// </summary>
public class MenuPermissionTreeRow
{
    public int MenuId { get; set; }
    public int? ParentId { get; set; }
    public string MenuName { get; set; } = string.Empty;

    /// <summary>Normalised page-path (claim key). Null/empty for group headers.</summary>
    public string? PagePath { get; set; }

    /// <summary>True when this menu maps to a real page (has a page-path).</summary>
    public bool IsPage { get; set; }

    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
    public bool Delete { get; set; }
    public bool Approve { get; set; }
    public bool Verify { get; set; }
    public bool Export { get; set; }
    public bool Print { get; set; }

    /// <summary>The actions rendered as tree columns, in display order.</summary>
    public static readonly Permission[] Actions =
    {
        Permission.View, Permission.Create, Permission.Edit, Permission.Delete,
        Permission.Approve, Permission.Verify, Permission.Export, Permission.Print,
    };

    public bool Get(Permission p) => p switch
    {
        Permission.View    => View,
        Permission.Create  => Create,
        Permission.Edit    => Edit,
        Permission.Delete  => Delete,
        Permission.Approve => Approve,
        Permission.Verify  => Verify,
        Permission.Export  => Export,
        Permission.Print   => Print,
        _ => false,
    };

    public void Set(Permission p, bool v)
    {
        switch (p)
        {
            case Permission.View:    View = v;    break;
            case Permission.Create:  Create = v;  break;
            case Permission.Edit:    Edit = v;    break;
            case Permission.Delete:  Delete = v;  break;
            case Permission.Approve: Approve = v; break;
            case Permission.Verify:  Verify = v;  break;
            case Permission.Export:  Export = v;  break;
            case Permission.Print:   Print = v;   break;
        }
    }

    /// <summary>Clears every action flag.</summary>
    public void ClearAll()
    {
        View = Create = Edit = Delete = Approve = Verify = Export = Print = false;
    }
}
