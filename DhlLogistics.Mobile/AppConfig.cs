namespace DhlLogistics.Mobile;

public static class AppConfig
{
    // Android emulator maps localhost to 10.0.2.2
    // iOS simulator uses localhost directly
    // Change to your deployed server URL for production
#if ANDROID
    public const string ApiBaseUrl = "http://10.0.2.2:5000";
#else
    public const string ApiBaseUrl = "http://localhost:5000";
#endif
}
