namespace DhlLogistics.Shared.Models;

public class Country
{
    public int Id { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty; // ISO 3166-1 alpha-2
    public bool IsActive { get; set; } = true;
}
