namespace DhlLogistics.Web.Workflow.Steps;

using DhlLogistics.Web.Service;
using Microsoft.Extensions.Logging;

// Side-effect steps run AFTER the transaction commits. They must never roll back a committed entity,
// so each is best-effort. This is also why "dashboard refresh" happens post-commit — counters then
// see live, committed data.

/// <summary>Step 6 — signal dashboards to reload their (live) counters after the commit.</summary>
public sealed class DashboardRefreshStep : IWorkflowStep
{
    private readonly DashboardState _dash;
    public DashboardRefreshStep(DashboardState dash) => _dash = dash;

    public string Name => "DashboardRefresh";

    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        _dash.NotifyChanged();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Step 7 — global-search index hook. Search in this app is a live query over committed tables
/// (<see cref="LogisticsService.GlobalSearchAsync"/>), so committing the entity is all that is needed;
/// there is no separate index to rebuild. This step is the extension point where an external indexer
/// (OpenSearch/Algolia/…) would be pushed to.
/// </summary>
public sealed class SearchIndexStep : IWorkflowStep
{
    private readonly ILogger<SearchIndexStep> _log;
    public SearchIndexStep(ILogger<SearchIndexStep> log) => _log = log;

    public string Name => "SearchIndex";

    public Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        _log.LogDebug("Search is live-query; nothing to index for {Module} #{Id}.",
            ctx.Handler.Module, ctx.Handler.Describe(ctx).EntityId);
        return Task.CompletedTask;
    }
}

/// <summary>Step 10 — notify managers/admins of the operation (best-effort; failures never roll back).</summary>
public sealed class NotificationStep : IWorkflowStep
{
    private readonly NotificationService _notify;
    private readonly ILogger<NotificationStep> _log;

    public NotificationStep(NotificationService notify, ILogger<NotificationStep> log)
    {
        _notify = notify;
        _log    = log;
    }

    public string Name => "Notification";

    public async Task RunAsync(IWorkflowContext ctx, CancellationToken ct = default)
    {
        try
        {
            var d = ctx.Handler.Describe(ctx);
            await _notify.NotifyManagersAsync(
                title: $"{ctx.Handler.Module} {ctx.Operation}d",
                body:  d.Summary,
                type:  ctx.Handler.EntityType);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Workflow notification failed (non-fatal) for {Module}.", ctx.Handler.Module);
        }
    }
}
