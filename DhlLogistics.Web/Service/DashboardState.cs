namespace DhlLogistics.Web.Service;

/// <summary>
/// App-wide change notifier for live dashboard counters (the "shared DashboardState
/// service" the Live Counters feature calls for). Registered as a <b>singleton</b> so a
/// record add/update/delete/status-change in any circuit raises one event that every
/// open Dashboard subscribes to — counters refresh with no page reload.
///
/// In-app mutation paths (<see cref="LogisticsService"/> CRUD, <see cref="JobAssignmentService"/>)
/// call <see cref="NotifyChanged"/> after they persist.
/// </summary>
public sealed class DashboardState
{
    /// <summary>Raised whenever counter-relevant data changes.</summary>
    public event Action? Changed;

    /// <summary>Signal subscribers (dashboards) to reload their counters.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
