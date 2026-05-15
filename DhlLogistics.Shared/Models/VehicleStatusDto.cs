namespace DhlLogistics.Shared.Models;

public class VehicleStatusDto
{
    public int    VehicleId        { get; set; }
    public string PlateNumber      { get; set; } = string.Empty;
    public string VehicleType      { get; set; } = string.Empty;
    public bool   IsActive         { get; set; }
    public string? CurrentJobCode  { get; set; }
    public string? CurrentJobStatus { get; set; }
    public string? AssignedExecutive { get; set; }
}
