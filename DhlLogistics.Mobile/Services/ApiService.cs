namespace DhlLogistics.Mobile.Services;

using DhlLogistics.Shared.Models;
using System.Net.Http.Json;

public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http) => _http = http;

    /// <summary>
    /// Fire-and-forget warmup of the Render free-tier container. Called from
    /// the login screen so the backend is spun up before the user navigates
    /// to a data-loading tab. Swallows all errors — purely opportunistic.
    /// </summary>
    public async Task WarmupAsync()
    {
        try   { await _http.GetAsync("/api/ping"); }
        catch { /* fire-and-forget */ }
    }

    // Jobs
    public Task<List<PickupJob>?> GetJobsAsync() =>
        _http.GetFromJsonAsync<List<PickupJob>>("/api/jobs");

    public Task<PickupJob?> GetJobAsync(int id) =>
        _http.GetFromJsonAsync<PickupJob>($"/api/jobs/{id}");

    public async Task<bool> UpdateJobStatusAsync(int id, JobStatus status)
    {
        var r = await _http.PutAsJsonAsync($"/api/jobs/{id}/status", new JobStatusUpdate { Status = status });
        return r.IsSuccessStatusCode;
    }

    // Dashboard
    public Task<DailyReportData?> GetDashboardAsync() =>
        _http.GetFromJsonAsync<DailyReportData>("/api/dashboard");

    public Task<List<UserActivityDto>?> GetUsersAsync() =>
        _http.GetFromJsonAsync<List<UserActivityDto>>("/api/dashboard/users");

    public Task<List<VehicleStatusDto>?> GetVehiclesAsync() =>
        _http.GetFromJsonAsync<List<VehicleStatusDto>>("/api/dashboard/vehicles");

    public Task<List<Container>?> GetCargoAsync() =>
        _http.GetFromJsonAsync<List<Container>>("/api/dashboard/cargo");

    // Activity reports (admin/manager only — returns 403 for other roles)
    public Task<ActivityReport?> GetReportDailyAsync() =>
        _http.GetFromJsonAsync<ActivityReport>("/api/admin/reports/daily");

    public Task<ActivityReport?> GetReportWeeklyAsync() =>
        _http.GetFromJsonAsync<ActivityReport>("/api/admin/reports/weekly");

    public Task<ActivityReport?> GetReportMonthlyAsync() =>
        _http.GetFromJsonAsync<ActivityReport>("/api/admin/reports/monthly");
}
