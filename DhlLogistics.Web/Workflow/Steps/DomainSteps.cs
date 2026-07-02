namespace DhlLogistics.Web.Workflow.Steps;

// The domain steps are thin, module-agnostic wrappers: they delegate to the context's handler so the
// same instances run for every module. Order and transaction boundaries are owned by the orchestrator.

/// <summary>Step 1 — validate the DTO/entity before any transaction is opened.</summary>
public sealed class ValidateStep : IWorkflowStep
{
    public string Name => "Validate";
    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default) => ctx.Handler.ValidateAsync(ctx);
}

/// <summary>Step 4 — generate the running number (Job Order No, …) when the operation requires it.</summary>
public sealed class GenerateNumberStep : IWorkflowStep
{
    public string Name => "GenerateNumber";
    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default) => ctx.Handler.GenerateNumberAsync(ctx);
}

/// <summary>Step 3 — save / update / delete the entity itself.</summary>
public sealed class PersistEntityStep : IWorkflowStep
{
    public string Name => "PersistEntity";
    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default) => ctx.Handler.PersistAsync(ctx);
}

/// <summary>Step 5 — create / update the linked billing records (JobId FK).</summary>
public sealed class GenerateBillingStep : IWorkflowStep
{
    public string Name => "GenerateBilling";
    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default) => ctx.Handler.GenerateBillingAsync(ctx);
}

/// <summary>Step 8 — append the module's workflow-timeline event.</summary>
public sealed class TimelineStep : IWorkflowStep
{
    public string Name => "WorkflowTimeline";
    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default) => ctx.Handler.WriteTimelineAsync(ctx);
}
