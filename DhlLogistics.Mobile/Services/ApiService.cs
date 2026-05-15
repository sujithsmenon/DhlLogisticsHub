namespace DhlLogistics.Mobile.Services;

using DhlLogistics.Shared.Models;
using System.Net.Http.Json;

public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http) => _http = http;

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
}
