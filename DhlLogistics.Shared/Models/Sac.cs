namespace DhlLogistics.Shared.Models;

// Services Accounting Code (Indian GST classification for services)
public class Sac
{
    public int Id { get; set; }
    public string SacCode { get; set; } = string.Empty; // 6-digit code
    public string Description { get; set; } = string.Empty;
    public decimal GstRate { get; set; }
    public bool IsActive { get; set; } = true;
}
