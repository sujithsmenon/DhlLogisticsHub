namespace DhlLogistics.Web.Api;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Service;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").RequireAuthorization("MobileApi");

        group.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var uid  = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = user.FindFirstValue(ClaimTypes.Role);
            var q    = db.Jobs.Include(j => j.AssignedVehicle).AsQueryable();
            if (role == "Executive")
                q = q.Where(j => j.AssignedExecutiveId == uid);
            return Results.Ok(await q.OrderByDescending(j => j.CreatedAt).ToListAsync());
        });

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var uid  = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = user.FindFirstValue(ClaimTypes.Role);
            var job  = await db.Jobs
                .Include(j => j.AssignedVehicle)
                .Include(j => j.GpsTrail)
                .FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            if (role == "Executive" && job.AssignedExecutiveId != uid) return Results.Forbid();
            return Results.Ok(job);
        });

        group.MapPut("/{id:int}/status", async (
            int id, JobStatusUpdate body,
            ClaimsPrincipal user,
            AppDbContext db,
            NotificationService notify) =>
        {
            var uid  = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = user.FindFirstValue(ClaimTypes.Role);

            var job = await db.Jobs.FindAsync(id);
            if (job is null) return Results.NotFound();
            if (role == "Executive" && job.AssignedExecutiveId != uid) return Results.Forbid();

            job.Status = body.Status;
            if (body.Status == JobStatus.InTransit) job.AssignedAt  = DateTime.UtcNow;
            if (body.Status == JobStatus.PickedUp)  job.PickedUpAt  = DateTime.UtcNow;
            if (body.Status == JobStatus.Stored)    job.StoredAt    = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // Notify managers of the status change
            await notify.NotifyManagersAsync(
                title:   $"Job {body.Status}",
                body:    $"{job.JobCode} is now {body.Status} — {job.ClientName}",
                type:    "StatusChanged",
                jobId:   job.Id,
                jobCode: job.JobCode);

            return Results.Ok(job);
        });
    }
}
