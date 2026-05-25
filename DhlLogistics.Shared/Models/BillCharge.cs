namespace DhlLogistics.Shared.Models;

public class BillCharge
{
    public long Id { get; set; }

    public long BillId { get; set; }
    public Bill? Bill   { get; set; }

    public int? ChargeCodeId { get; set; }
    public ChargeCode? ChargeCode { get; set; }

    public int? SacId { get; set; }
    public Sac? Sac    { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;
    public decimal Rate     { get; set; }
    public decimal Amount   { get; set; }     // Quantity * Rate (computed)

    public decimal GstRate   { get; set; }    // %
    public decimal GstAmount { get; set; }    // Amount * GstRate / 100
    public decimal NetAmount { get; set; }    // Amount + GstAmount

    public int DisplayOrder { get; set; }
}
