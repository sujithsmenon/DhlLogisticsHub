namespace DhlLogistics.Shared.Models;

/// <summary>
/// Per-page permission scopes. Mirrors CBM's UserPermission enum.
/// </summary>
public enum Permission
{
    View    = 1,
    Create  = 2,
    Edit    = 3,
    Delete  = 4,
    Approve = 5,
    Verify  = 6,
    Export  = 7,
    Print   = 8,
}

/// <summary>
/// Maps a (role, page-path, permission) triple to a granted flag.
/// PagePath is normalised — leading slash stripped, lowercased
/// (e.g. "masters/clients", "admin/permissions").
/// </summary>
public class RolePagePermission
{
    public int      Id           { get; set; }
    public string   RoleId       { get; set; } = string.Empty;
    public string   PagePath     { get; set; } = string.Empty;
    public Permission Permission { get; set; }
    public bool     IsGranted    { get; set; }
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;
    public string?  UpdatedBy    { get; set; }
}
