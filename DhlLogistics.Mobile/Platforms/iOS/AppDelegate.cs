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

    // Called when APNs issues a device token
    public override void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
    {
        // Convert token bytes to hex string
        var bytes  = deviceToken.ToArray();
        var token  = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        // Persist so PushService can pick it up and register with the server
        Preferences.Set("apns_device_token", token);

        // Notify any listener (PushService subscribes to this)
        MessagingCenter.Send(application, "APNsTokenReceived", token);
    }

    public override void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
        => System.Diagnostics.Debug.WriteLine($"[APNs] Registration failed: {error.LocalizedDescription}");

    // Foreground notification display (iOS 10+)
    public override void DidReceiveRemoteNotification(
        UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
    {
        completionHandler(UIBackgroundFetchResult.NewData);
    }
}
