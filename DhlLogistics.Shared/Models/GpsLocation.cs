namespace DhlLogistics.Shared.Models;

public class GpsLocation
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
