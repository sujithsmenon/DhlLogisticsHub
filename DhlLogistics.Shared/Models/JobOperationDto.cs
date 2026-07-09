namespace DhlLogistics.Shared.Models;

/// <summary>
/// Transport/read model for a <see cref="JobOperation"/>. Carries the resolved display names of
/// the related job / staff / vendor so callers (future UI, APIs) don't have to re-query, while
/// keeping the raw ids for round-tripping edits back through the service.
/// </summary>
public class JobOperationDto
{
    public long Id { get; set; }

    public long   JobOrderId { get; set; }
    public string? JobOrderNo { get; set; }

    public int    SequenceNo    { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string? Department   { get; set; }

    public JobOperationStatus Status     { get; set; } = JobOperationStatus.Draft;
    public string             StatusName => Status.ToString();
    public bool               CanGenerateCharges => Status != JobOperationStatus.Cancelled;
    public bool               IsBillable         => Status == JobOperationStatus.Completed;

    public JobOperationPriority Priority     { get; set; } = JobOperationPriority.Normal;
    public string               PriorityName => Priority.ToString();

    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate  { get; set; }

    public int?    AssignedStaffId   { get; set; }
    public string? AssignedStaffName { get; set; }

    public int?    VendorId   { get; set; }
    public string? VendorName { get; set; }

    public string? Remarks { get; set; }

    public string?   CreatedBy  { get; set; }
    public DateTime  CreatedOn  { get; set; }
    public string?   ModifiedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }

    public static JobOperationDto FromEntity(JobOperation o) => new()
    {
        Id                = o.Id,
        JobOrderId        = o.JobOrderId,
        JobOrderNo        = o.JobOrder?.JobOrderNo,
        SequenceNo        = o.SequenceNo,
        OperationType     = o.OperationType,
        Department        = o.Department,
        Status            = o.Status,
        Priority          = o.Priority,
        PlannedDate       = o.PlannedDate,
        ActualDate        = o.ActualDate,
        AssignedStaffId   = o.AssignedStaffId,
        AssignedStaffName = o.AssignedStaff?.FullName,
        VendorId          = o.VendorId,
        VendorName        = o.Vendor?.CompanyName,
        Remarks           = o.Remarks,
        CreatedBy         = o.CreatedBy,
        CreatedOn         = o.CreatedOn,
        ModifiedBy        = o.ModifiedBy,
        ModifiedOn        = o.ModifiedOn,
    };
}
