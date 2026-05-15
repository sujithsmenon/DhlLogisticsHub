namespace DhlLogistics.Shared.Models;

public class VehicleDriver
{
    public int Id { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LicenseNo { get; set; } = string.Empty;
    public DateTime? LicenseExpiry { get; set; }
    public int? AssignedVehicleId { get; set; }
    public Vehicle? AssignedVehicle { get; set; }
    public bool IsActive { get; set; } = true;
}
