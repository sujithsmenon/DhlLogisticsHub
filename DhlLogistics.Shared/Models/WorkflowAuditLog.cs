namespace DhlLogistics.Shared.Models;

/// <summary>The three mutating operations every module routes through the workflow engine.</summary>
public enum WorkflowOperationType
{
    Create = 1, Update = 2, Delete = 3,
    // Status-lifecycle transitions (run outside the orchestrator; logged via WorkflowLogService).
    Submit = 4, Verify = 5, Approve = 6, Reject = 7, Close = 8, Post = 9, Reopen = 10,
}

/// <summary>Distinguishes the user-facing "recent activity" feed row from the technical audit row.</summary>
public enum WorkflowLogKind { Activity = 1, Audit = 2 }

/// <summary>
/// Cross-cutting log written by the Workflow Engine's Activity + Audit steps. Deliberately holds a
/// loose (EntityType + EntityId) reference rather than a hard FK, so a row survives the deletion of
/// the entity it describes.
/// </summary>
public class WorkflowAuditLog
{
    public long Id { get; set; }

    public WorkflowLogKind Kind { get; set; }

    public string Module     { get; set; } = string.Empty;   // e.g. "Job Order"
    public string EntityType { get; set; } = string.Empty;   // e.g. "JobOrder"
    public long   EntityId   { get; set; }
    public string? EntityRef { get; set; }                   // e.g. "CLR/26-27/0001"

    public WorkflowOperationType Operation { get; set; }

    public string  Summary { get; set; } = string.Empty;
    public string? Details { get; set; }

    public string   Actor { get; set; } = "system";
    public DateTime At    { get; set; } = DateTime.UtcNow;
}
