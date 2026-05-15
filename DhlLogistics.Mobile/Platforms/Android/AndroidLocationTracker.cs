using Android.Content;
using DhlLogistics.Mobile.Services;

namespace DhlLogistics.Mobile;

public class AndroidLocationTracker : ILocationTracker
{
    private readonly AuthService _auth;

    public bool IsTracking { get; private set; }

    public AndroidLocationTracker(AuthService auth) => _auth = auth;

    public Task StartAsync(int jobId)
    {
        if (IsTracking) return Task.CompletedTask;

        var ctx    = Platform.AppContext;
        var intent = new Intent(ctx, typeof(LocationForegroundService));
        intent.SetAction(LocationForegroundService.ActionStart);
        intent.PutExtra(LocationForegroundService.ExtraJobId, jobId);
        intent.PutExtra(LocationForegroundService.ExtraJwt,   _auth.Token);

        ctx.StartForegroundService(intent);
        IsTracking = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!IsTracking) return Task.CompletedTask;

        var ctx    = Platform.AppContext;
        var intent = new Intent(ctx, typeof(LocationForegroundService));
        intent.SetAction(LocationForegroundService.ActionStop);
        ctx.StartService(intent);   // sends the STOP action to the running service

        IsTracking = false;
        return Task.CompletedTask;
    }
}
