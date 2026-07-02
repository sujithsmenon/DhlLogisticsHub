namespace DhlLogistics.Web.Workflow;

using DhlLogistics.Web.Database;
using DhlLogistics.Web.Workflow.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// The single pipeline every Add/Edit/Delete operation flows through:
/// <code>
/// UI → Application Service → WorkflowOrchestrator → DB transaction
/// </code>
/// In-transaction stages (number → persist → billing → timeline → activity → audit) commit atomically
/// or roll back together. Side-effect stages (dashboard, search, notification) run only after a
/// successful commit and are best-effort. All stages are reusable <see cref="IWorkflowStep"/>s;
/// module-specific behaviour lives entirely in the <see cref="IWorkflowHandler"/> on the context.
/// </summary>
public sealed class WorkflowOrchestrator
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkflowOrchestrator> _log;

    // In-transaction pipeline (ordered).
    private readonly ValidateStep       _validate;
    private readonly GenerateNumberStep _number;
    private readonly PersistEntityStep  _persist;
    private readonly GenerateBillingStep _billing;
    private readonly TimelineStep       _timeline;
    private readonly ActivityLogStep    _activity;
    private readonly AuditLogStep       _audit;

    // Post-commit side effects (ordered, best-effort).
    private readonly DashboardRefreshStep _dashboard;
    private readonly SearchIndexStep      _search;
    private readonly NotificationStep     _notification;

    public WorkflowOrchestrator(
        AppDbContext db, ILogger<WorkflowOrchestrator> log,
        ValidateStep validate, GenerateNumberStep number, PersistEntityStep persist,
        GenerateBillingStep billing, TimelineStep timeline, ActivityLogStep activity, AuditLogStep audit,
        DashboardRefreshStep dashboard, SearchIndexStep search, NotificationStep notification)
    {
        _db = db; _log = log;
        _validate = validate; _number = number; _persist = persist; _billing = billing;
        _timeline = timeline; _activity = activity; _audit = audit;
        _dashboard = dashboard; _search = search; _notification = notification;
    }

    public async Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        // 1. Validate before opening a transaction — cheap failure, nothing to undo.
        await _validate.RunAsync(ctx, ct);

        // 2. Begin transaction. Numbering runs before persist because entities carry their number at
        //    insert time (the spec lists persist-then-number, but the number must exist on the row).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _number.RunAsync(ctx, ct);     // 4. Generate Job Order No if required
            await _persist.RunAsync(ctx, ct);    // 3. Save / update / delete entity
            await _billing.RunAsync(ctx, ct);    // 5. Generate / update linked billing (JobId FK)
            await _timeline.RunAsync(ctx, ct);   // 8. Workflow timeline event
            await _activity.RunAsync(ctx, ct);   // 9. Recent activity
            await _audit.RunAsync(ctx, ct);      // 11. Audit log
            await tx.CommitAsync(ct);            // 12. Commit
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);          // 13. Roll back on any failure
            if (ex is not WorkflowException)
                _log.LogError(ex, "Workflow {Op} {Module} rolled back.", ctx.Operation, ctx.Handler.Module);
            throw;
        }

        // Post-commit side effects — counters see committed data; failures here never undo the commit.
        await _dashboard.RunAsync(ctx, ct);      // 6. Dashboard counters refresh AFTER commit
        await _search.RunAsync(ctx, ct);         // 7. Search index (live-query hook)
        await _notification.RunAsync(ctx, ct);   // 10. Notification (best-effort)
    }
}
