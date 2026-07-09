namespace DhlLogistics.Shared.Models;

/// <summary>
/// Workflow status of a single <see cref="JobOperation"/> line.
/// Draft → Assigned → In Progress → Completed, or Cancelled at any point.
/// Numeric spacing kept at 10s so old rows map cleanly (0 Draft, 20 In Progress, 30 Completed,
/// 40 Cancelled are unchanged; the AddJobOperationWorkflow migration remaps old 10 → 20).
/// </summary>
public enum JobOperationStatus
{
    Draft      = 0,
    Assigned   = 10,
    InProgress = 20,
    Completed  = 30,
    Cancelled  = 40,
}

/// <summary>
/// Handling priority of a single <see cref="JobOperation"/> line.
/// </summary>
public enum JobOperationPriority
{
    Normal = 0,   // CLR default → also the sensible default when unspecified
    Low    = 10,
    High   = 20,
    Urgent = 30,
}

/// <summary>
/// A planned/executed operation belonging to a <see cref="JobOrder"/> — the foundation for
/// tracking the multiple things a job needs done (Customs Clearance, Freight Booking,
/// Transportation, Warehousing, Documentation, Delivery, Insurance, Inspection, Packing, …).
///
/// This is a NEW, standalone tracking entity in its own table (<c>JobOperations</c>). It is
/// intentionally separate from the existing inline <see cref="JobOrderOperation"/> sub-grid
/// (which captures vendor cost lines edited within the JobOrder popup); nothing about the
/// existing JobOrder workflow, tables or billing is changed by adding this entity.
/// </summary>
public class JobOperation
{
    public long Id { get; set; }

    // ── Parent job (1 JobOrder ──< many JobOperation) ─────────────────────────
    public long JobOrderId { get; set; }
    public JobOrder? JobOrder { get; set; }

    /// <summary>Ordering of this operation within the job (1, 2, 3 …).</summary>
    public int SequenceNo { get; set; }

    /// <summary>Kind of operation, e.g. "Customs Clearance", "Freight Booking", "Transportation".</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>Owning department responsible for the operation (free-text for now).</summary>
    public string? Department { get; set; }

    public JobOperationStatus Status { get; set; } = JobOperationStatus.Draft;

    /// <summary>Handling priority for scheduling/attention (Low / Normal / High / Urgent).</summary>
    public JobOperationPriority Priority { get; set; } = JobOperationPriority.Normal;

    // ── Workflow rules (computed, not mapped) ─────────────────────────────────
    /// <summary>A cancelled operation can no longer generate billable charges.</summary>
    public bool CanGenerateCharges => Status != JobOperationStatus.Cancelled;

    /// <summary>A completed operation is the one that's eligible to be billed.</summary>
    public bool IsBillable => Status == JobOperationStatus.Completed;

    /// <summary>Scheduled date the operation is planned to be carried out.</summary>
    public DateTime? PlannedDate { get; set; }

    /// <summary>Date the operation was actually completed/executed.</summary>
    public DateTime? ActualDate { get; set; }

    /// <summary>Optional in-house staff member responsible for this operation.</summary>
    public int? AssignedStaffId { get; set; }
    public Staff? AssignedStaff { get; set; }

    /// <summary>Optional external vendor/organization carrying out this operation.</summary>
    public int? VendorId { get; set; }
    public DhlClient? Vendor { get; set; }

    public string? Remarks { get; set; }

    // ── Audit trail ───────────────────────────────────────────────────────────
    public string?  CreatedBy  { get; set; }
    public DateTime CreatedOn  { get; set; } = DateTime.UtcNow;
    public string?  ModifiedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
}
