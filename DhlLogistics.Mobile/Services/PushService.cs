using DhlLogistics.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

namespace DhlLogistics.Mobile.Services;

/// <summary>
/// Manages FCM/APNs token registration with the server and maintains the
/// SignalR NotificationHub connection for real-time in-app banners.
/// </summary>
public sealed class PushService : IAsyncDisposable
{
    private readonly HttpClient   _http;
    private readonly AuthService  _auth;
    private HubConnection?        _hub;

    public event Action<NotificationDto>? NotificationReceived;

    public PushService(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    // ── Server registration ──────────────────────────────────────────────────

    public async Task RegisterDeviceAsync()
    {
        if (!_auth.IsLoggedIn) return;

        var token    = GetPlatformToken();
        var platform = GetPlatformName();

        if (string.IsNullOrEmpty(token)) return;

        try
        {
            await _http.PostAsJsonAsync("/api/notifications/fcm/register",
                new { token, platform });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PushService] Register failed: {ex.Message}");
        }
    }

    // ── SignalR connection ───────────────────────────────────────────────────

    public async Task ConnectAsync()
    {
        if (!_auth.IsLoggedIn) return;

        _hub = new HubConnectionBuilder()
            .WithUrl($"{AppConfig.ApiBaseUrl}/notificationhub", opts =>
            {
                // Attach the JWT so the hub can authenticate the connection
                opts.AccessTokenProvider = () => Task.FromResult<string?>(
                    _auth.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationDto>("Notify", n =>
        {
            NotificationReceived?.Invoke(n);
        });

        try
        {
            await _hub.StartAsync();
            await _hub.SendAsync("JoinUserGroup", _auth.CurrentUserId, _auth.CurrentRole);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PushService] Hub connect failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
            await _hub.StopAsync();
    }

    // ── Platform token helpers ───────────────────────────────────────────────

    private static string? GetPlatformToken()
    {
#if ANDROID
        // FirebaseMessaging.Instance.GetToken() is called on Android at startup;
        // the result is stored in Preferences by the FirebaseMessagingService.
        return Preferences.Get("fcm_token", null);
#elif IOS
        return Preferences.Get("apns_device_token", null);
#else
        return null;
#endif
    }

    private static string GetPlatformName()
    {
#if ANDROID
        return "android";
#elif IOS
        return "ios";
#else
        return "unknown";
#endif
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
