namespace DhlLogistics.Shared.Models;

public class DriverDocumentType
{
    public int Id { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty; // DL, Aadhaar, PoliceVerify, Medical
    public bool HasExpiry { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
