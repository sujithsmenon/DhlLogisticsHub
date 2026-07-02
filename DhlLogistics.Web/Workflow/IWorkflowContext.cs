namespace DhlLogistics.Web.Workflow;

using DhlLogistics.Shared.Models;

/// <summary>
/// State bag threaded through every step of a single workflow run. One instance per Add/Edit/Delete
/// operation. Steps read the entity + operation, share data via <see cref="Items"/>, and delegate
/// module-specific work to <see cref="Handler"/>.
/// </summary>
public interface IWorkflowContext
{
    WorkflowOperationType Operation { get; }

    /// <summary>Username performing the operation (for audit / created-by / notifications).</summary>
    string User { get; }

    /// <summary>The domain entity being created / updated / deleted.</summary>
    object Entity { get; }

    /// <summary>Module adapter that supplies the domain-specific step implementations.</summary>
    IWorkflowHandler Handler { get; }

    /// <summary>When true, a delete is allowed to cascade its dependents instead of being blocked.</summary>
    bool CascadeDelete { get; }

    /// <summary>Free-form slots for passing data between steps (e.g. a generated number).</summary>
    IDictionary<string, object?> Items { get; }

    bool IsAborted { get; }
    string? AbortReason { get; }

    /// <summary>Fail the workflow with a user-facing reason (throws — the orchestrator rolls back).</summary>
    void Abort(string reason);
}
