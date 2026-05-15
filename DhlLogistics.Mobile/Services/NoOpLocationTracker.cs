namespace DhlLogistics.Mobile.Services;

/// <summary>Stub used on platforms without background GPS (iOS, Windows).</summary>
public sealed class NoOpLocationTracker : ILocationTracker
{
    public bool IsTracking => false;
    public Task StartAsync(int jobId) => Task.CompletedTask;
    public Task StopAsync()           => Task.CompletedTask;
}
