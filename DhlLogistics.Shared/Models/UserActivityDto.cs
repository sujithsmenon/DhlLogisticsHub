namespace DhlLogistics.Shared.Models;

public class UserActivityDto
{
    public string UserId      { get; set; } = string.Empty;
    public string FullName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string Role        { get; set; } = string.Empty;
    public bool   IsActive    { get; set; }
    public int    TotalJobs   { get; set; }
    public int    ActiveJobs  { get; set; }
    public int    CompletedJobs { get; set; }
    public DateTime? LastJobAt { get; set; }
}
