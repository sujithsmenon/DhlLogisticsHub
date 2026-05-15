namespace DhlLogistics.Web.Api;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Hub;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

public static class GpsEndpoints
{
    public static void MapGpsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gps").RequireAuthorization("MobileApi");

        // Mobile foreground service calls this every ~15 s even when app is closed
        group.MapPost("/update", async (
            GpsUpdateRequest body,
            ClaimsPrincipal user,
            AppDbContext db,
            IHubContext<GpsHub> hub) =>
        {
            // Persist the GPS point
            db.GpsLocations.Add(new GpsLocation
            {
                JobId      = body.JobId,
                Latitude   = body.Lat,
                Longitude  = body.Lng,
                Speed      = body.Speed,
                RecordedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            // Push to any dashboard clients watching this job in real-time
            await hub.Clients
                .Group($"job-{body.JobId}")
                .SendAsync("LocationUpdated", body.JobId, body.Lat, body.Lng, body.Speed, DateTime.UtcNow);

            return Results.Ok();
        });
    }
}
