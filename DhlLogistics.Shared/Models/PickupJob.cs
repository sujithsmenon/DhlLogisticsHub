namespace DhlLogistics.Shared.Models;

public class PickupJob
{
    public int Id { get; set; }
    public string JobCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupCity { get; set; } = string.Empty;
    public double VolumeCbm { get; set; }
    public double WeightKg { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string DhlReference { get; set; } = string.Empty;
    public DateTime? PickupDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedExecutiveId { get; set; }
    public int? AssignedVehicleId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime? PickedUpAt { get; set; }
    public DateTime? StoredAt { get; set; }

    public Vehicle? AssignedVehicle { get; set; }
    public List<GpsLocation> GpsTrail { get; set; } = [];
}

public enum JobStatus { Pending, Assigned, InTransit, PickedUp, Stored, Cancelled }
