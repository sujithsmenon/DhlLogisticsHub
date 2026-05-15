namespace DhlLogistics.Web.Database;

using Microsoft.AspNetCore.Identity;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public bool IsActive { get; set; } = true;
    public string? FcmToken { get; set; }
    public string? VehicleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
