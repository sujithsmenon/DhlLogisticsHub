namespace DhlLogistics.Shared.Models;

public class VehicleDocumentType
{
    public int Id { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty; // RC, Insurance, Permit, Fitness, PUC
    public bool HasExpiry { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
