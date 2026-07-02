namespace DhlLogistics.Shared.Models;

/// <summary>
/// Functional bucket a bill line belongs to. Lets one flat <see cref="BillCharge"/>
/// table cover every cost head the operations team bills for, without extra tables.
/// <see cref="Discount"/> lines are subtracted from the bill total.
/// </summary>
public enum ChargeCategory
{
    General       = 0,
    Labour        = 1,
    Freight       = 2,
    Transport     = 3,
    Customs       = 4,
    Documentation = 5,
    Port          = 6,
    Warehouse     = 7,
    Miscellaneous = 8,
    Tax           = 9,
    Discount      = 10,
}

public class BillCharge
{
    public long Id { get; set; }

    public long BillId { get; set; }
    public Bill? Bill   { get; set; }

    /// <summary>Functional bucket (Labour / Freight / Transport / Customs / …).</summary>
    public ChargeCategory Category { get; set; } = ChargeCategory.General;

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
