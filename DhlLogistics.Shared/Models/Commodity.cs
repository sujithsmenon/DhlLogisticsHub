namespace DhlLogistics.Shared.Models;

public class Commodity
{
    public int Id { get; set; }
    public string CommodityName { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty; // Harmonised System code
    public bool IsHazardous { get; set; }
    public bool IsActive { get; set; } = true;
}
