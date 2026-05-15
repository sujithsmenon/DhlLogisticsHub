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
        // set by AuthService is automatically used by ApiService
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.ApiBaseUrl),
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
