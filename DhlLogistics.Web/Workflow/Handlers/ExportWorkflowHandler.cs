namespace DhlLogistics.Web.Workflow.Handlers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;

/// <summary>
/// Workflow adapter for Export jobs. Only Create is an operational save (later steps are status
/// transitions handled by <see cref="Service.ExportJobService"/>); no numbering, no billing.
/// </summary>
public sealed class ExportWorkflowHandler : IWorkflowHandler
{
    private readonly AppDbContext _db;
    public ExportWorkflowHandler(AppDbContext db) => _db = db;

    // Display name only — the entity/table/route stay "ExportJob"/"/export" (module renamed 2026-07-17).
    public string Module     => "Sea Shipment";
    public string EntityType => "ExportJob";

    private static ExportJob Job(IWorkflowContext ctx) => (ExportJob)ctx.Entity;

    public Task ValidateAsync(IWorkflowContext ctx)
    {
        var job = Job(ctx);
        if (ctx.Operation == WorkflowOperationType.Create && string.IsNullOrWhiteSpace(job.CustomerName))
            ctx.Abort("Customer name is required.");
        return Task.CompletedTask;
    }

    public Task GenerateNumberAsync(IWorkflowContext ctx) => Task.CompletedTask;   // JobReference is user-entered

    public async Task PersistAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation != WorkflowOperationType.Create) return;

        var job = Job(ctx);
        job.Id         = 0;
        job.ReceivedAt = DateTime.UtcNow;
        job.Status     = ExportJobStatus.Received;
        _db.ExportJobs.Add(job);
        await _db.SaveChangesAsync();
    }

    public Task GenerateBillingAsync(IWorkflowContext ctx) => Task.CompletedTask;   // no billing for exports

    public async Task WriteTimelineAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation != WorkflowOperationType.Create) return;

        var job = Job(ctx);
        _db.Set<ExportJobEvent>().Add(new ExportJobEvent
        {
            ExportJobId   = job.Id,
            EventType     = "Created",
            Description   = $"Sea shipment created. Customer: {job.CustomerName}. Ref: {job.JobReference}.",
            CreatedByName = ctx.User,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public WorkflowDescriptor Describe(IWorkflowContext ctx)
    {
        var job  = Job(ctx);
        var verb = ctx.Operation.ToString().ToLowerInvariant() + "d";
        return new WorkflowDescriptor(job.Id, job.JobReference, $"{Module} {job.JobReference} ({job.CustomerName}) {verb}.");
    }
}
