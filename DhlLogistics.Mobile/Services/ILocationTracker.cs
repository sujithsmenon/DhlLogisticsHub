namespace DhlLogistics.Mobile.Services;

public interface ILocationTracker
{
    bool IsTracking { get; }

    /// <summary>Starts background GPS posting for the given job.</summary>
    Task StartAsync(int jobId);

    /// <summary>Stops the background GPS service.</summary>
    Task StopAsync();
}
