namespace DhlLogistics.Mobile;

public static class AppConfig
{
    // Production: AWS-hosted ASP.NET Core backend on the pvgt.co.in domain. Same
    // URL for every platform — real devices, simulators, and emulators all reach it
    // over the public internet.
    //
    // NOTE: this is HTTPS. The mobile app (and SignalR notification hub) require a
    // valid TLS certificate on pvgt.co.in. Ship a mobile build pointing here only
    // AFTER HTTPS is live on the domain (see docs/PVGT_TLS_IMPLEMENTATION.md). Until
    // then the old endpoint was: https://dhl-logistics-hub.onrender.com
    //
    // For local dev, swap to one of these:
    //   Android emulator → "http://10.0.2.2:5200"   (10.0.2.2 = host's localhost)
    //   iOS simulator    → "http://localhost:5200"
    //   Real device      → "http://<your-PC-LAN-IP>:5200"  (e.g. 192.168.1.42)
    public const string ApiBaseUrl = "https://pvgt.co.in";
}
