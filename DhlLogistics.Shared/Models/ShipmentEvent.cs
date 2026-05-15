namespace DhlLogistics.Shared.Models;

public class ShipmentEvent
{
    public int Id { get; set; }
    public int AwbShipmentId { get; set; }
    public AwbShipment? Shipment { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByName { get; set; }
}
