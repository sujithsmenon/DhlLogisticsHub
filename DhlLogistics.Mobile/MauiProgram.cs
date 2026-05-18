using DhlLogistics.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace DhlLogistics.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Single HttpClient instance shared by both services so the JWT token
        // set by AuthService is automatically used by ApiService.
        //
        // Explicit 60s timeout: Render free-tier cold-start can take ~30-45s.
        // The .NET default (100s) is too long — pages would hang and the user
        // would back-button out, silently cancelling the in-flight request
        // ("Socket closed" in logcat) and seeing an empty tab.
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.ApiBaseUrl),
            Timeout     = TimeSpan.FromSeconds(60),
        };
        builder.Services.AddSingleton(httpClient);
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<PushService>();

#if ANDROID
        builder.Services.AddSingleton<ILocationTracker, AndroidLocationTracker>();
#else
        builder.Services.AddSingleton<ILocationTracker, NoOpLocationTracker>();
#endif

        return builder.Build();
    }
}
