namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Service;

/// <summary>
/// Mobile API endpoints for the admin Daily/Weekly/Monthly activity report.
/// Same shape (<see cref="DhlLogistics.Shared.Models.ActivityReport"/>) used
/// by the web /admin/reports page.
/// </summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/reports").RequireAuthorization("MobileAdminApi");

        g.MapGet("/daily",   async (ReportService s) => Results.Ok(await s.GetDailyAsync()));
        g.MapGet("/weekly",  async (ReportService s) => Results.Ok(await s.GetWeeklyAsync()));
        g.MapGet("/monthly", async (ReportService s) => Results.Ok(await s.GetMonthlyAsync()));

        g.MapGet("/range", async (DateTime from, DateTime to, ReportService s) =>
        {
            if (to < from) (from, to) = (to, from);
            return Results.Ok(await s.GetRangeAsync(from, to));
        });
    }
}
