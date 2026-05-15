namespace DhlLogistics.Shared.Models;

public class NotificationDto
{
    public int      Id        { get; set; }
    public string   Title     { get; set; } = string.Empty;
    public string   Body      { get; set; } = string.Empty;
    public string   Type      { get; set; } = string.Empty;
    public int?     JobId     { get; set; }
    public string?  JobCode   { get; set; }
    public bool     IsRead    { get; set; }
    public DateTime CreatedAt { get; set; }
}
