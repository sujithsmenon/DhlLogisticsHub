namespace DhlLogistics.Shared.Models;

public class VehicleDocument
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int VehicleDocumentTypeId { get; set; }
    public VehicleDocumentType? VehicleDocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? FilePath { get; set; }
    public string? Remarks { get; set; }
}
