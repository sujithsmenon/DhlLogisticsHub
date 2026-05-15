namespace DhlLogistics.Shared.Models;

public enum PortType { Sea, Air, ICD, LandBorder }

public class Port
{
    public int Id { get; set; }
    public string PortName { get; set; } = string.Empty;
    public string PortCode { get; set; } = string.Empty; // INCOK, INMAA, INBOM etc.
    public PortType Type { get; set; } = PortType.Sea;
    public int? CountryId { get; set; }
    public Country? Country { get; set; }
    public string City { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
