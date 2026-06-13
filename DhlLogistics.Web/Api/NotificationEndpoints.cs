namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Database;
using DhlLogistics.Web.Model;
using DhlLogistics.Web.Service;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/notifications").RequireAuthorization("MobileApi");

        // GET /api/notifications — user's unread notifications
        g.MapGet("/", async (ClaimsPrincipal user, NotificationService svc) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uid is null) return Results.Unauthorized();
            return Results.Ok(await svc.GetForUserAsync(uid));
        });

        // PUT /api/notifications/{id}/read
        g.MapPut("/{id:int}/read", async (int id, NotificationService svc) =>
        {
            await svc.MarkReadAsync(id);
            return Results.Ok();
        });

        // POST /api/notifications/web-push/subscribe
        g.MapPost("/web-push/subscribe", async (
            WebPushSubscribeRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Upsert by endpoint
            var existing = await db.WebPushSubs.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
            if (existing is null)
            {
                db.WebPushSubs.Add(new WebPushSubscription
                {
                    UserId   = uid,
                    Endpoint = req.Endpoint,
                    P256dh   = req.P256dh,
                    Auth     = req.Auth,
                });
            }
            else
            {
                existing.UserId = uid;
                existing.P256dh = req.P256dh;
                existing.Auth   = req.Auth;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // POST /api/notifications/fcm/register
        g.MapPost("/fcm/register", async (
            FcmRegisterRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Upsert by token
            var existing = await db.FcmRegistrations.FirstOrDefaultAsync(r => r.Token == req.Token);
            if (existing is null)
            {
                db.FcmRegistrations.Add(new FcmRegistration
                {
                    UserId   = uid,
                    Token    = req.Token,
                    Platform = req.Platform,
                });
            }
            else
            {
                existing.UserId    = uid;
                existing.Platform  = req.Platform;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // GET /api/notifications/vapid-public-key  (needed by browser to subscribe)
        // Return PLAIN TEXT, not Results.Ok (which JSON-encodes the string and wraps it
        // in quotes). The browser reads this with response.text() and feeds it straight
        // to atob(); JSON quotes break the "GENERATE…" placeholder guard in dhlpush.js
        // and corrupt a real base64url key.
        app.MapGet("/api/notifications/vapid-public-key",
            (IConfiguration cfg) => Results.Text(cfg["WebPush:PublicKey"] ?? string.Empty))
           .AllowAnonymous();
    }

    public record WebPushSubscribeRequest(string Endpoint, string P256dh, string Auth);
    public record FcmRegisterRequest(string Token, string Platform);
}
