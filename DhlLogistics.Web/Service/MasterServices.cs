// Thin CRUD services for M2 masters. One class per entity to match CBM convention.
// All share the same shape so a generic base is used; subclasses just bind the DbSet.
//
// Each operation uses a SHORT-LIVED DbContext obtained from IDbContextFactory.
// Injecting a single circuit-scoped AppDbContext (the previous design) is the
// documented Blazor Server anti-pattern: one long-lived context is shared across
// every render/event in the circuit, which intermittently fails saves with
// "A second operation was started on this context instance" or stale-tracking
// errors. The factory matches the pattern already used everywhere else in the app.

namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public abstract class MasterServiceBase<T> where T : class
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly ILogger _log;

    // Bound to the current short-lived context for the duration of one operation
    // so the per-entity Set/Query overrides below stay tiny. Safe because Blazor
    // serialises a circuit's event handlers and each circuit gets its own instance.
    protected AppDbContext _db = null!;

    protected MasterServiceBase(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf)
    {
        _dbf = dbf;
        _log = lf.CreateLogger(GetType());
    }

    protected Task<AppDbContext> CreateDbAsync() => _dbf.CreateDbContextAsync();

    protected abstract DbSet<T> Set { get; }
    protected virtual IQueryable<T> Query() => Set.AsQueryable();

    public virtual async Task<List<T>> GetAllAsync()
    {
        await using var db = await CreateDbAsync();
        _db = db;
        return await Query().AsNoTracking().ToListAsync();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        await using var db = await CreateDbAsync();
        _db = db;
        return await Set.FindAsync(id);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await using var db = await CreateDbAsync();
        _db = db;
        try
        {
            db.Set<T>().Add(entity);
            await db.SaveChangesAsync();
            _log.LogInformation("Added {Entity}", typeof(T).Name);
            return entity;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Add {Entity} failed", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task UpdateAsync(T entity)
    {
        await using var db = await CreateDbAsync();
        _db = db;
        try
        {
            db.Update(entity);
            await db.SaveChangesAsync();
            _log.LogInformation("Updated {Entity}", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Update {Entity} failed", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<bool> DeleteAsync(int id)
    {
        await using var db = await CreateDbAsync();
        _db = db;
        try
        {
            var e = await db.Set<T>().FindAsync(id);
            if (e is null) return false;
            db.Set<T>().Remove(e);
            await db.SaveChangesAsync();
            _log.LogInformation("Deleted {Entity} id {Id}", typeof(T).Name, id);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Delete {Entity} id {Id} failed", typeof(T).Name, id);
            throw;
        }
    }
}

// ── Geography ────────────────────────────────────────────────────────────────

public class CountryService : MasterServiceBase<Country>
{
    public CountryService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Country> Set => _db.Countries;
    protected override IQueryable<Country> Query() => Set.OrderBy(c => c.CountryName);
}

public class RegionService : MasterServiceBase<Region>
{
    public RegionService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Region> Set => _db.Regions;
    protected override IQueryable<Region> Query() => Set.OrderBy(r => r.RegionName);
}

public class StateService : MasterServiceBase<State>
{
    public StateService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<State> Set => _db.States;
    protected override IQueryable<State> Query() => Set.Include(s => s.Region).OrderBy(s => s.StateName);
}

public class PortService : MasterServiceBase<Port>
{
    public PortService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Port> Set => _db.Ports;
    protected override IQueryable<Port> Query() => Set.Include(p => p.Country).OrderBy(p => p.PortName);
}

public class SezLocationService : MasterServiceBase<SezLocation>
{
    public SezLocationService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<SezLocation> Set => _db.SezLocations;
    protected override IQueryable<SezLocation> Query() =>
        Set.Include(s => s.Country).Include(s => s.State).OrderBy(s => s.LocationName);
}

// ── Finance / Tax ────────────────────────────────────────────────────────────

public class CurrencyService : MasterServiceBase<Currency>
{
    public CurrencyService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Currency> Set => _db.Currencies;
    protected override IQueryable<Currency> Query() => Set.OrderBy(c => c.CurrencyName);
}

public class SacService : MasterServiceBase<Sac>
{
    public SacService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Sac> Set => _db.Sacs;
    protected override IQueryable<Sac> Query() => Set.OrderBy(s => s.SacCode);
}

public class ChargeCodeService : MasterServiceBase<ChargeCode>
{
    public ChargeCodeService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<ChargeCode> Set => _db.ChargeCodes;
    protected override IQueryable<ChargeCode> Query() => Set.Include(c => c.Sac).OrderBy(c => c.ChargeName);
}

// ── Operations catalogues ────────────────────────────────────────────────────

public class ContainerSizeService : MasterServiceBase<ContainerSize>
{
    public ContainerSizeService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<ContainerSize> Set => _db.ContainerSizes;
    protected override IQueryable<ContainerSize> Query() => Set.OrderBy(c => c.SizeName);
}

public class CommodityService : MasterServiceBase<Commodity>
{
    public CommodityService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Commodity> Set => _db.Commodities;
    protected override IQueryable<Commodity> Query() => Set.OrderBy(c => c.CommodityName);
}

public class VesselService : MasterServiceBase<Vessel>
{
    public VesselService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Vessel> Set => _db.Vessels;
    protected override IQueryable<Vessel> Query() => Set.OrderBy(v => v.VesselName);
}

// ── Fleet ────────────────────────────────────────────────────────────────────

public class VehicleDriverService : MasterServiceBase<VehicleDriver>
{
    public VehicleDriverService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<VehicleDriver> Set => _db.VehicleDrivers;
    protected override IQueryable<VehicleDriver> Query() =>
        Set.Include(d => d.AssignedVehicle).OrderBy(d => d.DriverName);
}

public class VehicleDocumentTypeService : MasterServiceBase<VehicleDocumentType>
{
    public VehicleDocumentTypeService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<VehicleDocumentType> Set => _db.VehicleDocumentTypes;
    protected override IQueryable<VehicleDocumentType> Query() => Set.OrderBy(t => t.DocumentTypeName);
}

public class VehicleDocumentService : MasterServiceBase<VehicleDocument>
{
    public VehicleDocumentService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<VehicleDocument> Set => _db.VehicleDocuments;
    protected override IQueryable<VehicleDocument> Query() =>
        Set.Include(d => d.Vehicle).Include(d => d.VehicleDocumentType).OrderByDescending(d => d.ExpiryDate);
}

public class DriverDocumentTypeService : MasterServiceBase<DriverDocumentType>
{
    public DriverDocumentTypeService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<DriverDocumentType> Set => _db.DriverDocumentTypes;
    protected override IQueryable<DriverDocumentType> Query() => Set.OrderBy(t => t.DocumentTypeName);
}

// ── HR ───────────────────────────────────────────────────────────────────────

public class StaffDepartmentService : MasterServiceBase<StaffDepartment>
{
    public StaffDepartmentService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<StaffDepartment> Set => _db.StaffDepartments;
    protected override IQueryable<StaffDepartment> Query() => Set.OrderBy(d => d.DepartmentName);
}

public class StaffDesignationService : MasterServiceBase<StaffDesignation>
{
    public StaffDesignationService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<StaffDesignation> Set => _db.StaffDesignations;
    protected override IQueryable<StaffDesignation> Query() =>
        Set.Include(d => d.Department).OrderBy(d => d.DesignationName);
}

public class StaffService : MasterServiceBase<Staff>
{
    public StaffService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<Staff> Set => _db.Staff;
    protected override IQueryable<Staff> Query() =>
        Set.Include(s => s.Department).Include(s => s.Designation).OrderBy(s => s.FullName);
}

// ── CBM-parity stubs (needed by JobOrder filter row) ─────────────────────────

public class CompanyBranchService : MasterServiceBase<CompanyBranch>
{
    public CompanyBranchService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<CompanyBranch> Set => _db.CompanyBranches;
    protected override IQueryable<CompanyBranch> Query() => Set.OrderBy(b => b.BranchName);
}

public class ShipmentActivityService : MasterServiceBase<ShipmentActivity>
{
    public ShipmentActivityService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<ShipmentActivity> Set => _db.ShipmentActivities;
    protected override IQueryable<ShipmentActivity> Query() => Set.OrderBy(a => a.ActivityName);
}

// ── Accounts: Chart of accounts ──────────────────────────────────────────────

public class AccountHeadService : MasterServiceBase<AccountHead>
{
    public AccountHeadService(IDbContextFactory<AppDbContext> dbf, ILoggerFactory lf) : base(dbf, lf) { }
    protected override DbSet<AccountHead> Set => _db.AccountHeads;
    protected override IQueryable<AccountHead> Query() =>
        Set.OrderBy(a => a.Group).ThenBy(a => a.AccountCode);

    /// <summary>Heads flagged IsBank or IsCash — used in Receipt/Payment voucher popup.</summary>
    public async Task<List<AccountHead>> GetCashAndBankAsync()
    {
        await using var db = await CreateDbAsync();
        return await db.AccountHeads
            .Where(a => a.IsActive && (a.IsBank || a.IsCash))
            .OrderBy(a => a.AccountCode)
            .AsNoTracking()
            .ToListAsync();
    }
}
