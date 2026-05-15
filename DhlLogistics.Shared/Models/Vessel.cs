namespace DhlLogistics.Shared.Models;

public class Vessel
{
    public int Id { get; set; }
    public string VesselName { get; set; } = string.Empty;
    public string ImoNumber { get; set; } = string.Empty; // IMO XXXXXXX
    public string Flag { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
