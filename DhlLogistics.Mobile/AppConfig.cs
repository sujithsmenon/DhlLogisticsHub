namespace DhlLogistics.Mobile;

public static class AppConfig
{
    // Production: Render-hosted ASP.NET Core backend. Same URL for every
    // platform — real devices, simulators, and emulators all reach it over
    // public internet (HTTPS).
    //
    // For local dev, swap to one of these:
    //   Android emulator → "http://10.0.2.2:5000"   (10.0.2.2 = host's localhost)
    //   iOS simulator    → "http://localhost:5000"
    //   Real device      → "http://<your-PC-LAN-IP>:5000"  (e.g. 192.168.1.42)
    public const string ApiBaseUrl = "https://dhl-logistics-hub.onrender.com";
}
