using DhlLogistics.Web.Components.Pages;

namespace DhlLogistics.Web.Model
{
    public class PickupJob
    {
        public int Id { get; set; }
        public string JobCode { get; set; } = string.Empty;      // JOB-2024-001
        public string ClientName { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public string PickupCity { get; set; } = string.Empty;
        public double VolumeCbm { get; set; }
        public double WeightKg { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string DhlReference { get; set; } = string.Empty;
        public DateTime? PickupDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Assignment
        public string? AssignedExecutiveId { get; set; }          // AppUser.Id
        public string? AssignedVehicleId { get; set; }
        public DateTime? AssignedAt { get; set; }

        // Status
        public JobStatus Status { get; set; } = JobStatus.Pending;
        public DateTime? PickedUpAt { get; set; }
        public DateTime? StoredAt { get; set; }

        // Relations
        public AppUser? AssignedExecutive { get; set; }
        public Vehicle? AssignedVehicle { get; set; }
        public List<GpsLocation> GpsTrail { get; set; } = new();
    }

    public enum JobStatus { Pending, Assigned, InTransit, PickedUp, Stored, Cancelled }
}
