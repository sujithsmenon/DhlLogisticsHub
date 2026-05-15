namespace DhlLogistics.Shared.Models;

public class SezLocation
{
    public int Id { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int? StateId { get; set; }
    public State? State { get; set; }
    public int? CountryId { get; set; }
    public Country? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
