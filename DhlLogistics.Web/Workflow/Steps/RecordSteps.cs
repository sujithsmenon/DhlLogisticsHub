namespace DhlLogistics.Web.Workflow.Steps;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;

// These two steps run inside the transaction so the cross-cutting logs commit atomically with the
// entity. They are fully generic — they read everything they need from the handler's Describe().

/// <summary>Step 9 — write the user-facing "recent activity" row.</summary>
public sealed class ActivityLogStep : IWorkflowStep
{
    private readonly AppDbContext _db;
    public ActivityLogStep(AppDbContext db) => _db = db;

    public string Name => "RecentActivity";

    public async Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        var d = ctx.Handler.Describe(ctx);
        _db.Set<WorkflowAuditLog>().Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Activity,
            Module     = ctx.Handler.Module,
            EntityType = ctx.Handler.EntityType,
            EntityId   = d.EntityId,
            EntityRef  = d.Reference,
            Operation  = ctx.Operation,
            Summary    = d.Summary,
            Actor      = ctx.User,
            At         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Step 11 — write the technical audit row.</summary>
public sealed class AuditLogStep : IWorkflowStep
{
    private readonly AppDbContext _db;
    public AuditLogStep(AppDbContext db) => _db = db;

    public string Name => "AuditLog";

    public async Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        var d = ctx.Handler.Describe(ctx);
        _db.Set<WorkflowAuditLog>().Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Audit,
            Module     = ctx.Handler.Module,
            EntityType = ctx.Handler.EntityType,
            EntityId   = d.EntityId,
            EntityRef  = d.Reference,
            Operation  = ctx.Operation,
            Summary    = $"{ctx.Operation} {ctx.Handler.EntityType} #{d.EntityId} ({d.Reference}) by {ctx.User}.",
            Details    = d.Details,
            Actor      = ctx.User,
            At         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}
