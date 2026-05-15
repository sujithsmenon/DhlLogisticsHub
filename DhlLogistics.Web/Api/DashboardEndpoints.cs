namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Service;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/dashboard").RequireAuthorization("MobileApi");

        g.MapGet("/",        async (LogisticsService svc) => Results.Ok(await svc.get()));
        g.MapGet("/users",   async (LogisticsService svc) => Results.Ok(await svc.GetUserActivitiesAsync()));
        g.MapGet("/vehicles",async (LogisticsService svc) => Results.Ok(await svc.GetVehicleStatusesAsync()));
        g.MapGet("/cargo",   async (LogisticsService svc) => Results.Ok(await svc.GetContainersAsync()));
        g.MapGet("/jobs",    async (LogisticsService svc) => Results.Ok(await svc.GetRecentJobsAsync(20)));
    }
}
