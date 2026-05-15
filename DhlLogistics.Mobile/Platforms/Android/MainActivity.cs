using Android.App;
using Android.Content.PM;
using Android.OS;

namespace DhlLogistics.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int ReqLocation     = 1;
    private const int ReqNotification = 2;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Notification channel (Android 8+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O &&
            GetSystemService(NotificationService) is NotificationManager nm)
        {
            // High-priority channel for job alerts
            var alertCh = new NotificationChannel(
                "dhl_logistics", "DHL Logistics Alerts", NotificationImportance.High)
            {
                Description = "Job updates, assignments and dispatch alerts",
            };
            alertCh.EnableLights(true);
            alertCh.EnableVibration(true);
            nm.CreateNotificationChannel(alertCh);

            // Silent channel for the GPS persistent notification
            var gpsCh = new NotificationChannel(
                "dhl_gps", "DHL GPS Tracking", NotificationImportance.Low)
            {
                Description = "Live location tracking for active delivery jobs",
            };
            nm.CreateNotificationChannel(gpsCh);
        }

        RequestRuntimePermissions();
    }

    private void RequestRuntimePermissions()
    {
        var locationPerms = new List<string>
        {
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.AccessCoarseLocation,
        };

        // Background location must be requested separately after fine/coarse are granted
        // (Android 10+ policy: cannot bundle with other permissions)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            locationPerms.Add(Android.Manifest.Permission.AccessBackgroundLocation);

        RequestPermissions([.. locationPerms], ReqLocation);

        // Notification permission — Android 13+
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            RequestPermissions([Android.Manifest.Permission.PostNotifications], ReqNotification);
    }

    public override void OnRequestPermissionsResult(
        int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == ReqLocation)
        {
            // Log denied permissions so developers can debug in logcat
            for (int i = 0; i < permissions.Length; i++)
            {
                if (grantResults[i] != Permission.Granted)
                    System.Diagnostics.Debug.WriteLine(
                        $"[Permissions] Denied: {permissions[i]}");
            }
        }
    }
}
