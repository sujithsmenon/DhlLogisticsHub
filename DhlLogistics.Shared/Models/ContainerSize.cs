namespace DhlLogistics.Shared.Models;

public class ContainerSize
{
    public int Id { get; set; }
    public string SizeName { get; set; } = string.Empty; // 20ft, 40ft, 40HC, Reefer
    public string ShortCode { get; set; } = string.Empty; // 20GP, 40GP, 40HC, 20RF
    public decimal TeuFactor { get; set; } = 1m;          // 1 for 20ft, 2 for 40ft/40HC
    public decimal? PayloadKg { get; set; }
    public bool IsActive { get; set; } = true;
}
