namespace DhlLogistics.Web.Model;

public class AppNotification
{
    public int      Id        { get; set; }
    public string?  UserId    { get; set; }   // null = broadcast to all managers
    public string   Title     { get; set; } = string.Empty;
    public string   Body      { get; set; } = string.Empty;
    public string   Type      { get; set; } = string.Empty;
    public int?     JobId     { get; set; }
    public string?  JobCode   { get; set; }
    public bool     IsRead    { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
