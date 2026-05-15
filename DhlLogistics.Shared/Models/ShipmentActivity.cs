namespace DhlLogistics.Shared.Models;

public class ShipmentActivity
{
    public int Id { get; set; }
    public string ActivityCode { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
