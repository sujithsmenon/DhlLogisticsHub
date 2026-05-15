namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class LogisticsService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public LogisticsService(AppDbContext db, UserManager<AppUser> users)
    {
        _db    = db;
        _users = users;
    }

    public async Task<DailyReportData> get()
    {
        var now = DateTime.UtcNow;
        return new DailyReportData
        {
            TotalActiveContainers = await _db.Containers.CountAsync(c => c.Status == ContainerStatus.InUse),
            TotalShippedThisMonth = await _db.Jobs.CountAsync(j =>
                j.Status == JobStatus.Stored &&
                j.StoredAt.HasValue &&
                j.StoredAt.Value.Month == now.Month &&
                j.StoredAt.Value.Year  == now.Year),
            TotalPendingPickups   = await _db.Jobs.CountAsync(j =>
                j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned),
            TotalActiveClients    = await _db.Clients.CountAsync(),
        };
    }

    public async Task<List<PickupJob>> GetRecentJobsAsync(int count = 10) =>
        await _db.Jobs
            .Include(j => j.AssignedVehicle)
            .OrderByDescending(j => j.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<List<PickupJob>> GetAllJobsAsync(JobStatus? filter = null)
    {
        var q = _db.Jobs.Include(j => j.AssignedVehicle).AsQueryable();
        if (filter.HasValue)
            q = q.Where(j => j.Status == filter.Value);
        return await q.OrderByDescending(j => j.CreatedAt).ToListAsync();
    }

    public async Task<List<VehicleStatusDto>> GetVehicleStatusesAsync()
    {
        var vehicles = await _db.Vehicles.ToListAsync();
        var jobs     = await _db.Jobs
            .Where(j => j.Status == JobStatus.Assigned || j.Status == JobStatus.InTransit)
            .Include(j => j.AssignedVehicle)
            .ToListAsync();

        // Build a lookup: vehicleId → active job
        var jobByVehicle = jobs
            .Where(j => j.AssignedVehicleId.HasValue)
            .ToDictionary(j => j.AssignedVehicleId!.Value);

        // Build executor name lookup: userId → name
        var execIds = jobs
            .Where(j => j.AssignedExecutiveId != null)
            .Select(j => j.AssignedExecutiveId!)
            .Distinct()
            .ToList();

        var execNames = new Dictionary<string, string>();
        foreach (var id in execIds)
        {
            var u = await _users.FindByIdAsync(id);
            if (u is not null) execNames[id] = u.FullName;
        }

        return vehicles.Select(v =>
        {
            jobByVehicle.TryGetValue(v.Id, out var job);
            var execName = job?.AssignedExecutiveId is not null
                           && execNames.TryGetValue(job.AssignedExecutiveId, out var n)
                           ? n : null;
            return new VehicleStatusDto
            {
                VehicleId         = v.Id,
                PlateNumber       = v.PlateNumber,
                VehicleType       = v.VehicleType,
                IsActive          = v.IsActive,
                CurrentJobCode    = job?.JobCode,
                CurrentJobStatus  = job?.Status.ToString(),
                AssignedExecutive = execName,
            };
        }).ToList();
    }

    public async Task<List<UserActivityDto>> GetUserActivitiesAsync()
    {
        var appUsers = _users.Users.ToList();
        var result   = new List<UserActivityDto>();

        foreach (var u in appUsers)
        {
            var roles    = await _users.GetRolesAsync(u);
            var allJobs  = await _db.Jobs.CountAsync(j => j.AssignedExecutiveId == u.Id);
            var active   = await _db.Jobs.CountAsync(j =>
                j.AssignedExecutiveId == u.Id &&
                (j.Status == JobStatus.Assigned || j.Status == JobStatus.InTransit));
            var done     = await _db.Jobs.CountAsync(j =>
                j.AssignedExecutiveId == u.Id && j.Status == JobStatus.Stored);
            var lastJob  = await _db.Jobs
                .Where(j => j.AssignedExecutiveId == u.Id)
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => (DateTime?)j.CreatedAt)
                .FirstOrDefaultAsync();

            result.Add(new UserActivityDto
            {
                UserId        = u.Id,
                FullName      = u.FullName,
                Email         = u.Email ?? string.Empty,
                Role          = roles.FirstOrDefault() ?? "Viewer",
                IsActive      = u.IsActive,
                TotalJobs     = allJobs,
                ActiveJobs    = active,
                CompletedJobs = done,
                LastJobAt     = lastJob,
            });
        }

        return result;
    }

    public async Task<List<Container>> GetContainersAsync() =>
        await _db.Containers.ToListAsync();

    // ── Vehicles CRUD ────────────────────────────────────────────────────────
    public async Task<List<Vehicle>> GetAllVehiclesAsync() =>
        await _db.Vehicles.OrderBy(v => v.PlateNumber).ToListAsync();

    public async Task AddVehicleAsync(Vehicle v)
    {
        v.Id = 0;
        _db.Vehicles.Add(v);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateVehicleAsync(Vehicle v)
    {
        _db.Entry(v).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteVehicleAsync(int id)
    {
        var e = await _db.Vehicles.FindAsync(id);
        if (e is not null) { _db.Vehicles.Remove(e); await _db.SaveChangesAsync(); }
    }

    // ── Clients CRUD ─────────────────────────────────────────────────────────
    public async Task<List<DhlClient>> GetAllClientsAsync() =>
        await _db.Clients.OrderBy(c => c.CompanyName).ToListAsync();

    public async Task AddClientAsync(DhlClient c)
    {
        c.Id = 0;
        _db.Clients.Add(c);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateClientAsync(DhlClient c)
    {
        _db.Entry(c).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteClientAsync(int id)
    {
        var e = await _db.Clients.FindAsync(id);
        if (e is not null) { _db.Clients.Remove(e); await _db.SaveChangesAsync(); }
    }

    // ── Containers CRUD ──────────────────────────────────────────────────────
    public async Task AddContainerAsync(Container c)
    {
        c.Id = 0;
        _db.Containers.Add(c);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateContainerAsync(Container c)
    {
        _db.Entry(c).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteContainerAsync(int id)
    {
        var e = await _db.Containers.FindAsync(id);
        if (e is not null) { _db.Containers.Remove(e); await _db.SaveChangesAsync(); }
    }

    // ── Users CRUD ───────────────────────────────────────────────────────────
    public async Task<List<AppUser>> GetAllUsersAsync() =>
        await _users.Users.OrderBy(u => u.FullName).ToListAsync();

    public async Task<(bool Ok, string Error)> AddUserAsync(AppUser user, string password)
    {
        user.UserName = user.Email;
        user.CreatedAt = DateTime.UtcNow;
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        if (!string.IsNullOrWhiteSpace(user.Role))
            await _users.AddToRoleAsync(user, user.Role);
        return (true, "");
    }

    public async Task<(bool Ok, string Error)> UpdateUserAsync(AppUser patch)
    {
        var existing = await _users.FindByIdAsync(patch.Id);
        if (existing is null) return (false, "User not found.");
        existing.FullName = patch.FullName;
        existing.IsActive = patch.IsActive;
        var result = await _users.UpdateAsync(existing);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        if (existing.Role != patch.Role)
        {
            var roles = await _users.GetRolesAsync(existing);
            await _users.RemoveFromRolesAsync(existing, roles);
            await _users.AddToRoleAsync(existing, patch.Role);
            existing.Role = patch.Role;
            await _users.UpdateAsync(existing);
        }
        return (true, "");
    }

    public async Task DeleteUserAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is not null) await _users.DeleteAsync(u);
    }
}
