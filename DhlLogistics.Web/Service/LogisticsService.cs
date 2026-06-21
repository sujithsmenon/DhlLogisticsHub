namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class LogisticsService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly DashboardState _dash;

    public LogisticsService(AppDbContext db, UserManager<AppUser> users,
                            IDbContextFactory<AppDbContext> dbFactory, DashboardState dash)
    {
        _db        = db;
        _users     = users;
        _dbFactory = dbFactory;
        _dash      = dash;
    }

    // ── Global search ─────────────────────────────────────────────────────────
    // Searches the primary business entities + key masters. Uses a short-lived
    // context from the factory so rapid (debounced) calls never collide with the
    // circuit's scoped DbContext. Partial / exact / code matches via case-insensitive
    // ILike (Npgsql); exact & prefix matches are floated to the top.
    public async Task<List<GlobalSearchResult>> GlobalSearchAsync(string term, int take = 10)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length < 2) return new();
        var like = $"%{term}%";

        await using var db = await _dbFactory.CreateDbContextAsync();
        var results = new List<GlobalSearchResult>();

        results.AddRange(await db.Jobs.AsNoTracking()
            .Where(j => EF.Functions.ILike(j.JobCode, like)
                     || EF.Functions.ILike(j.ClientName, like)
                     || EF.Functions.ILike(j.DhlReference, like))
            .OrderByDescending(j => j.CreatedAt).Take(take)
            .Select(j => new GlobalSearchResult("Job", j.JobCode, j.ClientName, j.Status.ToString(), "/jobs"))
            .ToListAsync());

        results.AddRange(await db.AwbShipments.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.HawbNo, like)
                     || EF.Functions.ILike(a.ShipperName, like)
                     || EF.Functions.ILike(a.ConsigneeName, like)
                     || EF.Functions.ILike(a.ReferenceNumbers, like))
            .OrderByDescending(a => a.ReceivedAt).Take(take)
            .Select(a => new GlobalSearchResult("Shipment", a.HawbNo, a.ConsigneeName, a.Status.ToString(), "/awb"))
            .ToListAsync());

        results.AddRange(await db.Clients.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.CompanyName, like)
                     || EF.Functions.ILike(c.ContactEmail, like)
                     || EF.Functions.ILike(c.Phone, like))
            .OrderBy(c => c.CompanyName).Take(take)
            .Select(c => new GlobalSearchResult("Client", c.CompanyName, c.ContactEmail, null, "/masters/clients"))
            .ToListAsync());

        results.AddRange(await db.Containers.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.ContainerNumber, like)
                     || EF.Functions.ILike(c.ContainerType, like))
            .Take(take)
            .Select(c => new GlobalSearchResult("Container", c.ContainerNumber, c.ContainerType, c.Status.ToString(), "/masters/containers"))
            .ToListAsync());

        results.AddRange(await db.Staff.AsNoTracking()
            .Where(s => EF.Functions.ILike(s.FullName, like)
                     || EF.Functions.ILike(s.Email, like)
                     || EF.Functions.ILike(s.Phone, like))
            .OrderBy(s => s.FullName).Take(take)
            .Select(s => new GlobalSearchResult("Staff", s.FullName, s.Email, s.IsActive ? "Active" : "Inactive", "/staff"))
            .ToListAsync());

        results.AddRange(await db.AccountHeads.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.AccountCode, like)
                     || EF.Functions.ILike(a.AccountName, like))
            .OrderBy(a => a.AccountName).Take(take)
            .Select(a => new GlobalSearchResult("Account", a.AccountName, a.AccountCode, a.IsActive ? "Active" : "Inactive", "/accounts/heads"))
            .ToListAsync());

        results.AddRange(await db.Bills.AsNoTracking()
            .Where(b => EF.Functions.ILike(b.BillNo, like)
                     || (b.Reference != null && EF.Functions.ILike(b.Reference, like)))
            .OrderByDescending(b => b.CreatedOn).Take(take)
            .Select(b => new GlobalSearchResult("Invoice", b.BillNo, b.Reference, b.Status.ToString(), "/bills/clearance"))
            .ToListAsync());

        results.AddRange(await db.Vehicles.AsNoTracking()
            .Where(v => EF.Functions.ILike(v.PlateNumber, like)
                     || EF.Functions.ILike(v.VehicleType, like))
            .OrderBy(v => v.PlateNumber).Take(take)
            .Select(v => new GlobalSearchResult("Vehicle", v.PlateNumber, v.VehicleType, v.IsActive ? "Active" : "Inactive", "/masters/vehicles"))
            .ToListAsync());

        return results
            .OrderByDescending(r => string.Equals(r.Title, term, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToList();
    }

    public async Task<DailyReportData> get()
    {
        var now = DateTime.UtcNow;
        return new DailyReportData
        {
            // Active = operational inventory (everything not in Maintenance). The old
            // filter counted only InUse, so a fleet of Available containers showed 0.
            TotalActiveContainers = await _db.Containers.CountAsync(c => c.Status != ContainerStatus.Maintenance),
            TotalShippedThisMonth = await _db.Jobs.CountAsync(j =>
                j.Status == JobStatus.Stored &&
                j.StoredAt.HasValue &&
                j.StoredAt.Value.Month == now.Month &&
                j.StoredAt.Value.Year  == now.Year),
            TotalPendingPickups   = await _db.Jobs.CountAsync(j =>
                j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned),
            // Full-table count (was previously derived from only the last 12 jobs in the page).
            TotalInTransit        = await _db.Jobs.CountAsync(j => j.Status == JobStatus.InTransit),
            // Active Executives come from the Staff master, not from job assignments.
            TotalActiveExecutives = await _db.Staff.CountAsync(s => s.IsActive),
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

    // ── Containers / Vehicles / Clients CRUD ──────────────────────────────────
    // These use a SHORT-LIVED context from the factory (per operation), exactly like
    // MasterServiceBase. The previous design ran them on the long-lived circuit-scoped
    // _db: the grid/popup read tracked the entities, then the detached-entity Update
    // (Entry.State = Modified) threw "another instance with the same key is already
    // being tracked", so Edit-save failed and the grid showed stale tracked rows.
    public async Task<List<Container>> GetContainersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Containers.AsNoTracking().ToListAsync();
    }

    // ── Vehicles CRUD ────────────────────────────────────────────────────────
    public async Task<List<Vehicle>> GetAllVehiclesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Vehicles.AsNoTracking().OrderBy(v => v.PlateNumber).ToListAsync();
    }

    public async Task AddVehicleAsync(Vehicle v)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        v.Id = 0;
        db.Vehicles.Add(v);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task UpdateVehicleAsync(Vehicle v)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Vehicles.Update(v);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task DeleteVehicleAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Vehicles.FindAsync(id);
        if (e is not null) { db.Vehicles.Remove(e); await db.SaveChangesAsync(); _dash.NotifyChanged(); }
    }

    // ── Clients CRUD ─────────────────────────────────────────────────────────
    public async Task<List<DhlClient>> GetAllClientsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();
    }

    public async Task AddClientAsync(DhlClient c)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        c.Id = 0;
        db.Clients.Add(c);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task UpdateClientAsync(DhlClient c)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Clients.Update(c);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task DeleteClientAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Clients.FindAsync(id);
        if (e is not null) { db.Clients.Remove(e); await db.SaveChangesAsync(); _dash.NotifyChanged(); }
    }

    // ── Containers CRUD ──────────────────────────────────────────────────────
    public async Task AddContainerAsync(Container c)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        c.Id = 0;
        db.Containers.Add(c);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task UpdateContainerAsync(Container c)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Containers.Update(c);
        await db.SaveChangesAsync();
        _dash.NotifyChanged();
    }

    public async Task DeleteContainerAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Containers.FindAsync(id);
        if (e is not null) { db.Containers.Remove(e); await db.SaveChangesAsync(); _dash.NotifyChanged(); }
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
        _dash.NotifyChanged();
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
        _dash.NotifyChanged();
        return (true, "");
    }

    public async Task DeleteUserAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is not null) { await _users.DeleteAsync(u); _dash.NotifyChanged(); }
    }
}

/// <summary>One global-search hit shown in the top-bar results dropdown.</summary>
/// <param name="EntityType">Display category (Job, Shipment, Client, …).</param>
/// <param name="Title">Primary name / reference / code.</param>
/// <param name="Subtitle">Secondary context (client, type, email) — may be null.</param>
/// <param name="Status">Status text where the entity has one — may be null.</param>
/// <param name="Url">Route to navigate to on click.</param>
public record GlobalSearchResult(string EntityType, string Title, string? Subtitle, string? Status, string Url);
