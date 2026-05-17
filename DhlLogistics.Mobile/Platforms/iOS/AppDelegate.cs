using Foundation;
using UIKit;
using UserNotifications;

namespace DhlLogistics.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Request APNs permission and register for remote notifications
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                if (granted)
                    MainThread.BeginInvokeOnMainThread(
                        () => UIApplication.SharedApplication.RegisterForRemoteNotifications());
            });

        return base.FinishedLaunching(application, launchOptions);
    }

    // ── APNs callbacks ───────────────────────────────────────────────────
    // These are protocol methods on UIApplicationDelegate, not virtual on
    // MauiUIApplicationDelegate — bind them by Objective-C selector via
    // [Export] instead of `override`.

    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
    {
        var bytes = deviceToken.ToArray();
        var token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        // PushService.GetPlatformToken() reads this key on demand.
        Preferences.Set("apns_device_token", token);
    }

    [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
    public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
        => System.Diagnostics.Debug.WriteLine($"[APNs] Registration failed: {error.LocalizedDescription}");

    [Export("application:didReceiveRemoteNotification:fetchCompletionHandler:")]
    public void DidReceiveRemoteNotification(
        UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
    {
        completionHandler(UIBackgroundFetchResult.NewData);
    }
}
