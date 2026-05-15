namespace DhlLogistics.Shared.Models;

public class EmailLog
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool HasAttachment { get; set; }
    public bool JobCreated { get; set; }
}
