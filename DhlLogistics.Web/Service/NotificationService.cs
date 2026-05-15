namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Hub;
using DhlLogistics.Web.Model;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebPush;

public class NotificationService
{
    private readonly AppDbContext              _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IConfiguration            _config;

    public NotificationService(
        AppDbContext db,
        IHubContext<NotificationHub> hub,
        IConfiguration config)
    {
        _db     = db;
        _hub    = hub;
        _config = config;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Notify a specific user (e.g., executive whose job was assigned).</summary>
    public Task NotifyUserAsync(string userId, string title, string body,
                                string type, int? jobId = null, string? jobCode = null)
        => SendAsync(userId, false, title, body, type, jobId, jobCode);

    /// <summary>Broadcast to all Managers and Admins (e.g., new job created).</summary>
    public Task NotifyManagersAsync(string title, string body,
                                    string type, int? jobId = null, string? jobCode = null)
        => SendAsync(null, true, title, body, type, jobId, jobCode);

    /// <summary>Mark a notification as read.</summary>
    public async Task MarkReadAsync(int notificationId)
    {
        var n = await _db.Notifications.FindAsync(notificationId);
        if (n is null) return;
        n.IsRead = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>Return unread notifications for a user (personal + broadcast).</summary>
    public Task<List<AppNotification>> GetForUserAsync(string userId) =>
        _db.Notifications
           .Where(n => !n.IsRead && (n.UserId == userId || n.UserId == null))
           .OrderByDescending(n => n.CreatedAt)
           .Take(50)
           .ToListAsync();

    // ── Internal delivery ────────────────────────────────────────────────────

    private async Task SendAsync(
        string? userId, bool broadcast,
        string title, string body, string type,
        int? jobId, string? jobCode)
    {
        // 1. Persist
        var record = new AppNotification
        {
            UserId    = userId,
            Title     = title,
            Body      = body,
            Type      = type,
            JobId     = jobId,
            JobCode   = jobCode,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Notifications.Add(record);
        await _db.SaveChangesAsync();

        var dto = ToDto(record);

        // 2. SignalR (instant, works when app is open)
        if (broadcast)
            await _hub.Clients.Group("managers").SendAsync("Notify", dto);
        else if (userId is not null)
            await _hub.Clients.Group($"user-{userId}").SendAsync("Notify", dto);

        // 3. Web Push (browser, even when tab is closed)
        await SendWebPushAsync(userId, broadcast, title, body, jobCode);

        // 4. FCM (Android + iOS, when app is closed)
        await SendFcmAsync(userId, broadcast, title, body, jobCode);
    }

    // ── Web Push (VAPID) ─────────────────────────────────────────────────────

    private async Task SendWebPushAsync(
        string? userId, bool broadcast, string title, string body, string? jobCode)
    {
        var vapid = _config.GetSection("WebPush");
        var subject  = vapid["Subject"];
        var pubKey   = vapid["PublicKey"];
        var privKey  = vapid["PrivateKey"];

        if (string.IsNullOrEmpty(pubKey) || string.IsNullOrEmpty(privKey)) return;

        var subs = broadcast
            ? await _db.WebPushSubs.ToListAsync()
            : await _db.WebPushSubs.Where(s => s.UserId == userId).ToListAsync();

        if (!subs.Any()) return;

        var client  = new WebPushClient();
        var details = new VapidDetails(subject ?? "mailto:admin@dhllogistics.com", pubKey, privKey);
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            body,
            url   = jobCode != null ? $"/jobs" : "/",
            badge = "/favicon.png",
            icon  = "/favicon.png",
        });

        foreach (var sub in subs)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, details);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription expired — remove it
                _db.WebPushSubs.Remove(sub);
            }
            catch { /* ignore transient errors */ }
        }

        await _db.SaveChangesAsync();
    }

    // ── Firebase Cloud Messaging (FCM) ───────────────────────────────────────

    private async Task SendFcmAsync(
        string? userId, bool broadcast, string title, string body, string? jobCode)
    {
        if (FirebaseApp.DefaultInstance is null) return;   // Firebase not configured

        var tokens = broadcast
            ? await _db.FcmRegistrations.Select(r => r.Token).ToListAsync()
            : await _db.FcmRegistrations.Where(r => r.UserId == userId).Select(r => r.Token).ToListAsync();

        if (!tokens.Any()) return;

        var data = new Dictionary<string, string>
        {
            ["type"]    = "job_notification",
            ["jobCode"] = jobCode ?? string.Empty,
            ["url"]     = jobCode != null ? "/jobs" : "/",
        };

        // FCM allows up to 500 tokens per MulticastMessage
        foreach (var batch in tokens.Chunk(500))
        {
            var message = new MulticastMessage
            {
                Tokens       = batch.ToList(),
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body  = body,
                },
                Data    = data,
                Android = new AndroidConfig
                {
                    Priority     = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "dhl_logistics",
                        Color     = "#D40511",
                    },
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Badge = 1, Sound = "default" },
                },
            };

            try { await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message); }
            catch { /* log in production */ }
        }
    }

    private static NotificationDto ToDto(AppNotification n) => new()
    {
        Id        = n.Id,
        Title     = n.Title,
        Body      = n.Body,
        Type      = n.Type,
        JobId     = n.JobId,
        JobCode   = n.JobCode,
        IsRead    = n.IsRead,
        CreatedAt = n.CreatedAt,
    };
}
