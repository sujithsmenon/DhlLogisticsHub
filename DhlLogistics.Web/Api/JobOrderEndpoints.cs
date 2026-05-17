namespace DhlLogistics.Web.Api;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Service;

/// <summary>
/// Read-only JobOrder endpoints that mirror the four lifecycle-stage lists
/// (Clearance / Forwarding / Verify / Approve) on the web dashboard.
/// Mutations stay on the web app.
/// </summary>
public static class JobOrderEndpoints
{
    public static void MapJobOrderEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/joborders").RequireAuthorization("MobileAdminApi");

        // ── Lifecycle-stage lists (mirror the 4 web pages) ───────────────────
        g.MapGet("/clearance",   async (JobOrderService s) => Results.Ok(await s.GetByModeAsync(JobMode.Clearance)));
        g.MapGet("/forwarding",  async (JobOrderService s) => Results.Ok(await s.GetByModeAsync(JobMode.Forwarding)));

        // Verify page on web shows Submitted jobs only.
        g.MapGet("/verify",      async (JobOrderService s) => Results.Ok(await s.GetByStatusAsync(JobOrderStatus.Submitted)));

        // Approve page on web shows Verified jobs only.
        g.MapGet("/approve",     async (JobOrderService s) => Results.Ok(await s.GetByStatusAsync(JobOrderStatus.Verified)));

        // ── Filterable list + detail ─────────────────────────────────────────
        // Optional ?mode=Clearance|Forwarding and ?status=Draft|Submitted|... (repeatable).
        g.MapGet("/", async (JobOrderService s, string? mode, string[]? status) =>
        {
            if (status is { Length: > 0 })
            {
                var parsed = status
                    .Select(v => Enum.TryParse<JobOrderStatus>(v, true, out var s2) ? (JobOrderStatus?)s2 : null)
                    .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                return Results.Ok(await s.GetByStatusAsync(parsed));
            }
            if (!string.IsNullOrEmpty(mode) && Enum.TryParse<JobMode>(mode, true, out var m))
                return Results.Ok(await s.GetByModeAsync(m));

            // Default: every status — easier than enumerating; matches "show all" on web.
            return Results.Ok(await s.GetByStatusAsync(
                JobOrderStatus.Draft, JobOrderStatus.Submitted, JobOrderStatus.Verified,
                JobOrderStatus.Approved, JobOrderStatus.Rejected,
                JobOrderStatus.Closed, JobOrderStatus.Reopened));
        });

        g.MapGet("/{id:long}",   async (long id, JobOrderService s) =>
            await s.GetByIdAsync(id) is { } j ? Results.Ok(j) : Results.NotFound());
    }
}
