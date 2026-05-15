#if ANDROID
using Android.App;
using Firebase.Messaging;

namespace DhlLogistics.Mobile;

/// <summary>
/// Receives FCM tokens and foreground messages on Android.
/// The token is persisted to Preferences so PushService can read it.
/// </summary>
[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public class DhlFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        Preferences.Set("fcm_token", token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        // App is in foreground — show a local notification
        var notif = message.GetNotification();
        if (notif is null) return;

        var nm      = (Android.App.NotificationManager?)GetSystemService(NotificationService);
        var builder = new Android.App.Notification.Builder(this, "dhl_logistics")
            .SetContentTitle(notif.Title)
            .SetContentText(notif.Body)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetAutoCancel(true);

        nm?.Notify(System.Environment.TickCount, builder.Build());
    }
}
#endif
