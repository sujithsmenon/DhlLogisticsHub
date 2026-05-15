namespace DhlLogistics.Shared.Models;

public class ChargeCode
{
    public int Id { get; set; }
    public string ChargeName { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public int? SacId { get; set; }
    public Sac? Sac { get; set; }
    public decimal DefaultAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
