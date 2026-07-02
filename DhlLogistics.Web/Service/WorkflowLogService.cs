namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes the cross-cutting <see cref="WorkflowAuditLog"/> Activity + Audit rows (and, optionally, a
/// manager notification) for status-lifecycle transitions — Submit / Verify / Approve / Reject / Close /
/// Post. Those transitions run as direct service methods OUTSIDE the <see cref="Workflow.WorkflowOrchestrator"/>
/// (which only covers Create/Update/Delete and whose ActivityLogStep/AuditLogStep/NotificationStep would
/// otherwise never fire for them). This reuses the exact same log schema those steps write, so the audit
/// trail is uniform. It participates in the caller's transaction (its <c>SaveChangesAsync</c> is enlisted
/// when a transaction is open), so the log commits atomically with the transition.
/// </summary>
public class WorkflowLogService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly ILogger<WorkflowLogService> _log;

    public WorkflowLogService(AppDbContext db, NotificationService notify, ILogger<WorkflowLogService> log)
    {
        _db = db;
        _notify = notify;
        _log = log;
    }

    public async Task LogTransitionAsync(
        string module, string entityType, long entityId, string? entityRef,
        WorkflowOperationType op, string actor, string summary, bool notifyManagers = false)
    {
        var now = DateTime.UtcNow;

        _db.Set<WorkflowAuditLog>().Add(new WorkflowAuditLog
        {
            Kind = WorkflowLogKind.Activity, Module = module, EntityType = entityType, EntityId = entityId,
            EntityRef = entityRef, Operation = op, Summary = summary, Actor = actor, At = now,
        });
        _db.Set<WorkflowAuditLog>().Add(new WorkflowAuditLog
        {
            Kind = WorkflowLogKind.Audit, Module = module, EntityType = entityType, EntityId = entityId,
            EntityRef = entityRef, Operation = op,
            Summary = $"{op} {entityType} #{entityId} ({entityRef}) by {actor}.", Actor = actor, At = now,
        });
        await _db.SaveChangesAsync();

        if (notifyManagers)
        {
            // Best-effort: a notification/push failure must never roll back the transition.
            try { await _notify.NotifyManagersAsync($"{module} {op}d", summary, entityType); }
            catch (Exception ex) { _log.LogWarning(ex, "Transition notification failed (non-fatal) for {Module}.", module); }
        }
    }
}
