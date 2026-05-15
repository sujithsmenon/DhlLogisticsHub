namespace DhlLogistics.Shared.Models;

public class Region
{
    public int Id { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
