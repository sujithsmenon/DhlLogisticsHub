namespace DhlLogistics.Shared.Models;

public class PickupInfo
{
    public string ClientName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupCity { get; set; } = string.Empty;
    public DateTime? PickupDate { get; set; }
    public double VolumeCbm { get; set; }
    public double WeightKg { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string DhlReference { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
}
