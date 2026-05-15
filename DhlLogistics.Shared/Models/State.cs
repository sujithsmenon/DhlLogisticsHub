namespace DhlLogistics.Shared.Models;

public class State
{
    public int Id { get; set; }
    public string StateName { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty; // IN-KL, IN-TN etc.
    public int? RegionId { get; set; }
    public Region? Region { get; set; }
    public bool IsActive { get; set; } = true;
}
