namespace DhlLogistics.Shared.Models;

/// <summary>
/// A single business operation carried out within a job order (e.g. "Customs Clearance",
/// "Transport", "Warehousing"). Optionally sub-contracted to another organization and/or
/// handled by an in-house staff member. Child rows are edited inline in the JobOrder popup
/// and persisted together with the parent <see cref="JobOrder"/> (replace-on-update).
/// </summary>
public class JobOrderOperation
{
    public long Id { get; set; }

    public long JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    /// <summary>Free-text business operation, e.g. "Customs Clearance".</summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>Optional: operation sub-contracted to another organization (client).</summary>
    public int? OperatedByClientId { get; set; }
    public DhlClient? OperatedByClient { get; set; }

    /// <summary>Optional: in-house staff member handling this operation.</summary>
    public int? HandledByStaffId { get; set; }
    public Staff? HandledByStaff { get; set; }

    /// <summary>Vendor cost we pay <see cref="OperatedByClient"/> for this operation. When set with a
    /// vendor, job approval auto-posts an expense/payable voucher (Dr Expense, Cr Accounts Payable).</summary>
    public decimal? Cost { get; set; }

    /// <summary>Which expense head the <see cref="Cost"/> posts to (Transport → Transportation Expense,
    /// Customs → Customs Duty, Port → Port Charges, Labour/Documentation → their heads, else Misc).</summary>
    public ChargeCategory ExpenseCategory { get; set; } = ChargeCategory.General;

    public string? Remarks { get; set; }

    public int DisplayOrder { get; set; }
}
