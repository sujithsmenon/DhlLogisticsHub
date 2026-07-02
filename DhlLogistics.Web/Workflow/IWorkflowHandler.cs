namespace DhlLogistics.Web.Workflow;

/// <summary>
/// Module adapter that plugs a specific entity (Job Order, AWB, Export, …) into the shared pipeline.
/// The orchestrator + steps stay generic; each module supplies one handler implementing the
/// domain-specific stages. Cross-cutting stages (activity, audit, dashboard, notification) use
/// <see cref="Describe"/> so they need no per-module code.
/// </summary>
public interface IWorkflowHandler
{
    /// <summary>Human-readable module name, e.g. "Job Order".</summary>
    string Module { get; }

    /// <summary>Stable entity type key, e.g. "JobOrder".</summary>
    string EntityType { get; }

    /// <summary>Validate the DTO/entity; call <see cref="IWorkflowContext.Abort"/> to reject.</summary>
    Task ValidateAsync(IWorkflowContext ctx);

    /// <summary>Generate the running number (Job Order No, Bill No, …) when the operation requires one.</summary>
    Task GenerateNumberAsync(IWorkflowContext ctx);

    /// <summary>Insert / update / delete the entity itself.</summary>
    Task PersistAsync(IWorkflowContext ctx);

    /// <summary>Create or update the linked billing records (no-op for modules without billing).</summary>
    Task GenerateBillingAsync(IWorkflowContext ctx);

    /// <summary>Append the module's workflow-timeline event (append-only history).</summary>
    Task WriteTimelineAsync(IWorkflowContext ctx);

    /// <summary>Supply id / reference / summary for the generic activity, audit and notification steps.</summary>
    WorkflowDescriptor Describe(IWorkflowContext ctx);
}

/// <summary>Compact description of the entity a workflow run touched, used by the cross-cutting steps.</summary>
public readonly record struct WorkflowDescriptor(long EntityId, string? Reference, string Summary, string? Details = null);
