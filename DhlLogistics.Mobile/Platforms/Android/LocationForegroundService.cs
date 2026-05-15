using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DhlLogistics.Shared.Models;

namespace DhlLogistics.Mobile;

[Service(
    Name = "com.dhl.logistics.LocationForegroundService",
    ForegroundServiceType = ForegroundService.TypeLocation,
    Exported = false)]
public class LocationForegroundService : Service
{
    public const string ActionStart  = "DHL_GPS_START";
    public const string ActionStop   = "DHL_GPS_STOP";
    public const string ExtraJobId   = "job_id";
    public const string ExtraJwt     = "jwt";

    private const int NotifId        = 1001;
    private const int IntervalMs     = 15_000;   // 15 s

    private Timer?      _timer;
    private HttpClient? _http;
    private int         _jobId;
    private LocationManager? _lm;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopTracking();
            return StartCommandResult.NotSticky;
        }

        // Recover state from intent (first start) or Preferences (OS restart)
        _jobId = intent?.GetIntExtra(ExtraJobId, 0)
                 ?? Preferences.Get("gps_job_id", 0);
        var jwt = intent?.GetStringExtra(ExtraJwt)
                  ?? Preferences.Get("gps_jwt", null);

        if (_jobId == 0 || string.IsNullOrEmpty(jwt))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        // Persist so the service can recover after an OS-initiated restart
        Preferences.Set("gps_job_id", _jobId);
        Preferences.Set("gps_jwt",    jwt);

        _http = new HttpClient { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);

        _lm = (LocationManager?)GetSystemService(LocationService);

        ShowForegroundNotification();
        StartTimer();

        return StartCommandResult.Sticky;   // OS restarts the service if killed
    }

    public override void OnDestroy()
    {
        StopTracking();
        base.OnDestroy();
    }

    // ── GPS loop ─────────────────────────────────────────────────────────────

    private void StartTimer()
    {
        _timer?.Dispose();
        _timer = new Timer(async _ => await PostLocationAsync(), null, 0, IntervalMs);
    }

    private async Task PostLocationAsync()
    {
        try
        {
            var loc = GetBestLocation();
            if (loc is null) return;

            await _http!.PostAsJsonAsync("/api/gps/update", new GpsUpdateRequest(
                JobId: _jobId,
                Lat:   loc.Latitude,
                Lng:   loc.Longitude,
                Speed: loc.HasSpeed ? loc.Speed : 0));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] Post failed: {ex.Message}");
        }
    }

    private Android.Locations.Location? GetBestLocation()
    {
        if (_lm is null) return null;

        // Prefer GPS provider, fall back to network
        var gps = _lm.GetLastKnownLocation(LocationManager.GpsProvider);
        var net = _lm.GetLastKnownLocation(LocationManager.NetworkProvider);

        if (gps is null) return net;
        if (net is null) return gps;

        // Return whichever fix is more recent
        return gps.Time >= net.Time ? gps : net;
    }

    // ── Foreground notification (required by Android 8+) ────────────────────

    private void ShowForegroundNotification()
    {
        var nm = (NotificationManager?)GetSystemService(NotificationService);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                "dhl_gps",
                "DHL GPS Tracking",
                NotificationImportance.Low)   // Low = silent, no heads-up
            {
                Description = "Live location tracking for active delivery jobs",
            };
            nm?.CreateNotificationChannel(channel);
        }

        var notification = new Notification.Builder(this, "dhl_gps")
            .SetContentTitle("DHL — Live Tracking Active")
            .SetContentText("Your location is being shared for active job delivery.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .Build();

        // TypeLocation required on Android 14+ (API 34+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            StartForeground(NotifId, notification, ForegroundService.TypeLocation);
        else
            StartForeground(NotifId, notification);
    }

    // ── Stop helpers ─────────────────────────────────────────────────────────

    private void StopTracking()
    {
        _timer?.Dispose();
        _timer = null;
        _http?.Dispose();
        _http = null;

        // Clear persisted state
        Preferences.Remove("gps_job_id");
        Preferences.Remove("gps_jwt");

        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }
}
