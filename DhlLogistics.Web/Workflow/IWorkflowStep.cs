namespace DhlLogistics.Web.Workflow;

/// <summary>
/// One independent, reusable stage of the workflow pipeline (validate, generate number, persist,
/// bill, log, notify, …). Steps are module-agnostic: anything domain-specific is delegated to
/// <see cref="IWorkflowContext.Handler"/>, so the same step instances run for every module.
/// </summary>
public interface IWorkflowStep
{
    string Name { get; }

    Task RunAsync(IWorkflowContext context, CancellationToken ct = default);
}
