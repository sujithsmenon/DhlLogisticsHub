namespace DhlLogistics.Shared.Models;

/// <summary>
/// Application-side profile row for an ASP.NET Identity user. Ported from CBM's
/// <c>RegisterdUser</c> (spelling kept to match the reference). Bridges an
/// <c>AspNetUsers</c> row (<see cref="AspNetUserId"/>) to domain data — the linked
/// <see cref="Staff"/> member — and acts as the parent key for the per-user activity
/// and branch permission tables.
/// </summary>
public class RegisterdUser
{
    /// <summary>Surrogate PK used by the user permission link tables.</summary>
    public int UserId { get; set; }

    /// <summary>FK to <c>AspNetUsers.Id</c> (string GUID).</summary>
    public string AspNetUserId { get; set; } = string.Empty;

    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }

    public int? StaffId { get; set; }
    public Staff? Staff { get; set; }
}
