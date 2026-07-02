namespace DhlLogistics.Web.Workflow;

using DhlLogistics.Shared.Models;

/// <summary>Thrown when a workflow step aborts (validation failure, delete blocked, …).</summary>
public sealed class WorkflowException : Exception
{
    public WorkflowException(string message) : base(message) { }
}

/// <inheritdoc />
public sealed class WorkflowContext : IWorkflowContext
{
    public WorkflowContext(WorkflowOperationType operation, string user, object entity,
                           IWorkflowHandler handler, bool cascadeDelete = false)
    {
        Operation     = operation;
        User          = user;
        Entity        = entity;
        Handler       = handler;
        CascadeDelete = cascadeDelete;
    }

    public WorkflowOperationType Operation { get; }
    public string User { get; }
    public object Entity { get; }
    public IWorkflowHandler Handler { get; }
    public bool CascadeDelete { get; }
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    public bool IsAborted { get; private set; }
    public string? AbortReason { get; private set; }

    public void Abort(string reason)
    {
        IsAborted   = true;
        AbortReason = reason;
        throw new WorkflowException(reason);
    }
}
