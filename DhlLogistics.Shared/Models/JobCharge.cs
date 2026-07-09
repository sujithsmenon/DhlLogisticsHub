namespace DhlLogistics.Shared.Models;

/// <summary>
/// A billable sale charge line captured on a <see cref="JobOrder"/> (Qty × Rate + GST). These are the
/// customer-facing charges the operations team agrees on the job — distinct from
/// <see cref="JobOrderOperation.Cost"/>, which is the vendor COST we pay and posts to expense/payable.
/// On job approval every JobCharge is copied 1:1 into the linked bill's <see cref="BillCharge"/> rows
/// (see <c>BillService.UpsertForJobAsync</c>), so the auto-generated bill already carries the full
/// charge set, totals and GST — nothing has to be re-keyed on the bill. Edited inline in the JobOrder
/// popup and persisted with the parent (replace-on-update, same as bill charges).
/// </summary>
public class JobCharge
{
    public long Id { get; set; }

    public long JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    /// <summary>Optional owning operation (Customs Clearance / Freight / Transportation / …). Lets each
    /// <see cref="JobOperation"/> own its own charge lines; copied onto the bill so billing can group by
    /// operation. Null = a general job-level charge not tied to any single operation.</summary>
    public long? JobOperationId { get; set; }
    public JobOperation? JobOperation { get; set; }

    /// <summary>Functional bucket (Labour / Freight / Transport / Customs / …). Mirrors <see cref="BillCharge.Category"/>.</summary>
    public ChargeCategory Category { get; set; } = ChargeCategory.General;

    public int? ChargeCodeId { get; set; }
    public ChargeCode? ChargeCode { get; set; }

    public int? SacId { get; set; }
    public Sac? Sac { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;
    public decimal Rate     { get; set; }
    public decimal Amount   { get; set; }     // Quantity * Rate (computed)

    public decimal GstRate   { get; set; }    // %
    public decimal GstAmount { get; set; }    // Amount * GstRate / 100
    public decimal NetAmount { get; set; }    // Amount + GstAmount

    public int DisplayOrder { get; set; }
}
