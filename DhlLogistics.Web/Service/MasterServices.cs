// Thin CRUD services for M2 masters. One class per entity to match CBM convention.
// All share the same shape so a generic base is used; subclasses just bind the DbSet.

namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

public abstract class MasterServiceBase<T> where T : class
{
    protected readonly AppDbContext _db;
    protected MasterServiceBase(AppDbContext db) => _db = db;

    protected abstract DbSet<T> Set { get; }
    protected virtual IQueryable<T> Query() => Set.AsQueryable();

    public virtual async Task<List<T>> GetAllAsync()    => await Query().ToListAsync();
    public virtual async Task<T?>      GetByIdAsync(int id) => await Set.FindAsync(id);

    public virtual async Task<T> AddAsync(T entity)
    {
        _db.Set<T>().Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public virtual async Task<bool> DeleteAsync(int id)
    {
        var e = await Set.FindAsync(id);
        if (e is null) return false;
        Set.Remove(e);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ── Geography ────────────────────────────────────────────────────────────────

public class CountryService : MasterServiceBase<Country>
{
    public CountryService(AppDbContext db) : base(db) { }
    protected override DbSet<Country> Set => _db.Countries;
    protected override IQueryable<Country> Query() => Set.OrderBy(c => c.CountryName);
}

public class RegionService : MasterServiceBase<Region>
{
    public RegionService(AppDbContext db) : base(db) { }
    protected override DbSet<Region> Set => _db.Regions;
    protected override IQueryable<Region> Query() => Set.OrderBy(r => r.RegionName);
}

public class StateService : MasterServiceBase<State>
{
    public StateService(AppDbContext db) : base(db) { }
    protected override DbSet<State> Set => _db.States;
    protected override IQueryable<State> Query() => Set.Include(s => s.Region).OrderBy(s => s.StateName);
}

public class PortService : MasterServiceBase<Port>
{
    public PortService(AppDbContext db) : base(db) { }
    protected override DbSet<Port> Set => _db.Ports;
    protected override IQueryable<Port> Query() => Set.Include(p => p.Country).OrderBy(p => p.PortName);
}

public class SezLocationService : MasterServiceBase<SezLocation>
{
    public SezLocationService(AppDbContext db) : base(db) { }
    protected override DbSet<SezLocation> Set => _db.SezLocations;
    protected override IQueryable<SezLocation> Query() =>
        Set.Include(s => s.Country).Include(s => s.State).OrderBy(s => s.LocationName);
}

// ── Finance / Tax ────────────────────────────────────────────────────────────

public class CurrencyService : MasterServiceBase<Currency>
{
    public CurrencyService(AppDbContext db) : base(db) { }
    protected override DbSet<Currency> Set => _db.Currencies;
    protected override IQueryable<Currency> Query() => Set.OrderBy(c => c.CurrencyName);
}

public class SacService : MasterServiceBase<Sac>
{
    public SacService(AppDbContext db) : base(db) { }
    protected override DbSet<Sac> Set => _db.Sacs;
    protected override IQueryable<Sac> Query() => Set.OrderBy(s => s.SacCode);
}

public class ChargeCodeService : MasterServiceBase<ChargeCode>
{
    public ChargeCodeService(AppDbContext db) : base(db) { }
    protected override DbSet<ChargeCode> Set => _db.ChargeCodes;
    protected override IQueryable<ChargeCode> Query() => Set.Include(c => c.Sac).OrderBy(c => c.ChargeName);
}

// ── Operations catalogues ────────────────────────────────────────────────────

public class ContainerSizeService : MasterServiceBase<ContainerSize>
{
    public ContainerSizeService(AppDbContext db) : base(db) { }
    protected override DbSet<ContainerSize> Set => _db.ContainerSizes;
    protected override IQueryable<ContainerSize> Query() => Set.OrderBy(c => c.SizeName);
}

public class CommodityService : MasterServiceBase<Commodity>
{
    public CommodityService(AppDbContext db) : base(db) { }
    protected override DbSet<Commodity> Set => _db.Commodities;
    protected override IQueryable<Commodity> Query() => Set.OrderBy(c => c.CommodityName);
}

public class VesselService : MasterServiceBase<Vessel>
{
    public VesselService(AppDbContext db) : base(db) { }
    protected override DbSet<Vessel> Set => _db.Vessels;
    protected override IQueryable<Vessel> Query() => Set.OrderBy(v => v.VesselName);
}

// ── Fleet ────────────────────────────────────────────────────────────────────

public class VehicleDriverService : MasterServiceBase<VehicleDriver>
{
    public VehicleDriverService(AppDbContext db) : base(db) { }
    protected override DbSet<VehicleDriver> Set => _db.VehicleDrivers;
    protected override IQueryable<VehicleDriver> Query() =>
        Set.Include(d => d.AssignedVehicle).OrderBy(d => d.DriverName);
}

public class VehicleDocumentTypeService : MasterServiceBase<VehicleDocumentType>
{
    public VehicleDocumentTypeService(AppDbContext db) : base(db) { }
    protected override DbSet<VehicleDocumentType> Set => _db.VehicleDocumentTypes;
    protected override IQueryable<VehicleDocumentType> Query() => Set.OrderBy(t => t.DocumentTypeName);
}

public class VehicleDocumentService : MasterServiceBase<VehicleDocument>
{
    public VehicleDocumentService(AppDbContext db) : base(db) { }
    protected override DbSet<VehicleDocument> Set => _db.VehicleDocuments;
    protected override IQueryable<VehicleDocument> Query() =>
        Set.Include(d => d.Vehicle).Include(d => d.VehicleDocumentType).OrderByDescending(d => d.ExpiryDate);
}

public class DriverDocumentTypeService : MasterServiceBase<DriverDocumentType>
{
    public DriverDocumentTypeService(AppDbContext db) : base(db) { }
    protected override DbSet<DriverDocumentType> Set => _db.DriverDocumentTypes;
    protected override IQueryable<DriverDocumentType> Query() => Set.OrderBy(t => t.DocumentTypeName);
}

// ── HR ───────────────────────────────────────────────────────────────────────

public class StaffDepartmentService : MasterServiceBase<StaffDepartment>
{
    public StaffDepartmentService(AppDbContext db) : base(db) { }
    protected override DbSet<StaffDepartment> Set => _db.StaffDepartments;
    protected override IQueryable<StaffDepartment> Query() => Set.OrderBy(d => d.DepartmentName);
}

public class StaffDesignationService : MasterServiceBase<StaffDesignation>
{
    public StaffDesignationService(AppDbContext db) : base(db) { }
    protected override DbSet<StaffDesignation> Set => _db.StaffDesignations;
    protected override IQueryable<StaffDesignation> Query() =>
        Set.Include(d => d.Department).OrderBy(d => d.DesignationName);
}

public class StaffService : MasterServiceBase<Staff>
{
    public StaffService(AppDbContext db) : base(db) { }
    protected override DbSet<Staff> Set => _db.Staff;
    protected override IQueryable<Staff> Query() =>
        Set.Include(s => s.Department).Include(s => s.Designation).OrderBy(s => s.FullName);
}

// ── CBM-parity stubs (needed by JobOrder filter row) ─────────────────────────

public class CompanyBranchService : MasterServiceBase<CompanyBranch>
{
    public CompanyBranchService(AppDbContext db) : base(db) { }
    protected override DbSet<CompanyBranch> Set => _db.CompanyBranches;
    protected override IQueryable<CompanyBranch> Query() => Set.OrderBy(b => b.BranchName);
}

public class ShipmentActivityService : MasterServiceBase<ShipmentActivity>
{
    public ShipmentActivityService(AppDbContext db) : base(db) { }
    protected override DbSet<ShipmentActivity> Set => _db.ShipmentActivities;
    protected override IQueryable<ShipmentActivity> Query() => Set.OrderBy(a => a.ActivityName);
}
