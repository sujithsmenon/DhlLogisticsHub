namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain
    public DbSet<Container>        Containers    => Set<Container>();
    public DbSet<Collection>       Collections   => Set<Collection>();
    public DbSet<DhlClient>        Clients       => Set<DhlClient>();
    public DbSet<PickupJob>        Jobs          => Set<PickupJob>();
    public DbSet<GpsLocation>      GpsLocations  => Set<GpsLocation>();
    public DbSet<EmailLog>         EmailLogs     => Set<EmailLog>();
    public DbSet<Vehicle>          Vehicles      => Set<Vehicle>();

    // AWB Shipment workflow
    public DbSet<AwbShipment>   AwbShipments  => Set<AwbShipment>();
    public DbSet<Transporter>   Transporters  => Set<Transporter>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();

    // Export Job workflow
    public DbSet<ExportJob>      ExportJobs       => Set<ExportJob>();
    public DbSet<ExportJobEvent> ExportJobEvents  => Set<ExportJobEvent>();

    // Notifications
    public DbSet<AppNotification>    Notifications     => Set<AppNotification>();
    public DbSet<WebPushSubscription> WebPushSubs      => Set<WebPushSubscription>();
    public DbSet<FcmRegistration>    FcmRegistrations  => Set<FcmRegistration>();

    // ── M2 Masters: Geography ────────────────────────────────────────────────
    public DbSet<Country>     Countries  => Set<Country>();
    public DbSet<Region>      Regions    => Set<Region>();
    public DbSet<State>       States     => Set<State>();
    public DbSet<Port>        Ports      => Set<Port>();
    public DbSet<SezLocation> SezLocations => Set<SezLocation>();

    // ── M2 Masters: Finance / Tax ────────────────────────────────────────────
    public DbSet<Currency>   Currencies   => Set<Currency>();
    public DbSet<Sac>        Sacs         => Set<Sac>();
    public DbSet<ChargeCode> ChargeCodes  => Set<ChargeCode>();

    // ── M2 Masters: Operations catalogues ────────────────────────────────────
    public DbSet<ContainerSize> ContainerSizes => Set<ContainerSize>();
    public DbSet<Commodity>     Commodities    => Set<Commodity>();
    public DbSet<Vessel>        Vessels        => Set<Vessel>();

    // ── M2 Masters: Fleet ────────────────────────────────────────────────────
    public DbSet<VehicleDriver>       VehicleDrivers       => Set<VehicleDriver>();
    public DbSet<VehicleDocumentType> VehicleDocumentTypes => Set<VehicleDocumentType>();
    public DbSet<VehicleDocument>     VehicleDocuments     => Set<VehicleDocument>();
    public DbSet<DriverDocumentType>  DriverDocumentTypes  => Set<DriverDocumentType>();

    // ── M2 Masters: HR ───────────────────────────────────────────────────────
    public DbSet<StaffDepartment>  StaffDepartments  => Set<StaffDepartment>();
    public DbSet<StaffDesignation> StaffDesignations => Set<StaffDesignation>();
    public DbSet<Staff>            Staff             => Set<Staff>();

    // ── M3 Permissions ───────────────────────────────────────────────────────
    public DbSet<RolePagePermission> RolePagePermissions => Set<RolePagePermission>();

    // ── M4 Job Orders ────────────────────────────────────────────────────────
    public DbSet<JobOrder>         JobOrders         => Set<JobOrder>();
    public DbSet<JobOrderEvent>    JobOrderEvents    => Set<JobOrderEvent>();
    public DbSet<CompanyBranch>    CompanyBranches   => Set<CompanyBranch>();
    public DbSet<ShipmentActivity> ShipmentActivities => Set<ShipmentActivity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Decimal precision
        mb.Entity<Currency>().Property(c => c.ExchangeRateToInr).HasPrecision(18, 6);
        mb.Entity<ContainerSize>().Property(c => c.TeuFactor).HasPrecision(8, 2);
        mb.Entity<ContainerSize>().Property(c => c.PayloadKg).HasPrecision(10, 2);
        mb.Entity<Sac>().Property(s => s.GstRate).HasPrecision(5, 2);
        mb.Entity<ChargeCode>().Property(c => c.DefaultAmount).HasPrecision(18, 2);

        // Relationships
        mb.Entity<State>()
            .HasOne(s => s.Region).WithMany().HasForeignKey(s => s.RegionId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Port>()
            .HasOne(p => p.Country).WithMany().HasForeignKey(p => p.CountryId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<SezLocation>()
            .HasOne(s => s.Country).WithMany().HasForeignKey(s => s.CountryId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<SezLocation>()
            .HasOne(s => s.State).WithMany().HasForeignKey(s => s.StateId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<ChargeCode>()
            .HasOne(c => c.Sac).WithMany().HasForeignKey(c => c.SacId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<VehicleDriver>()
            .HasOne(d => d.AssignedVehicle).WithMany().HasForeignKey(d => d.AssignedVehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<VehicleDocument>()
            .HasOne(d => d.Vehicle).WithMany().HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<VehicleDocument>()
            .HasOne(d => d.VehicleDocumentType).WithMany().HasForeignKey(d => d.VehicleDocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<StaffDesignation>()
            .HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Staff>()
            .HasOne(s => s.Department).WithMany().HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Staff>()
            .HasOne(s => s.Designation).WithMany().HasForeignKey(s => s.DesignationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique indexes on natural keys (codes)
        mb.Entity<Country>().HasIndex(c => c.CountryCode).IsUnique();
        mb.Entity<Currency>().HasIndex(c => c.CurrencyCode).IsUnique();
        mb.Entity<Port>().HasIndex(p => p.PortCode).IsUnique();
        mb.Entity<Sac>().HasIndex(s => s.SacCode).IsUnique();
        mb.Entity<State>().HasIndex(s => s.StateCode);

        // M3: unique (Role, Page, Permission) and lookup index
        mb.Entity<RolePagePermission>()
            .HasIndex(p => new { p.RoleId, p.PagePath, p.Permission }).IsUnique();
        mb.Entity<RolePagePermission>()
            .HasIndex(p => new { p.RoleId, p.PagePath });

        // ── M4 Job Orders ────────────────────────────────────────────────────
        mb.Entity<JobOrder>().Property(j => j.LclUnits).HasPrecision(12, 3);
        mb.Entity<JobOrder>().Property(j => j.GrossWeightKg).HasPrecision(12, 3);
        mb.Entity<JobOrder>().Property(j => j.VolumeCbm).HasPrecision(12, 3);
        mb.Entity<JobOrder>().Property(j => j.EstimatedValue).HasPrecision(18, 2);

        mb.Entity<JobOrder>()
            .HasOne(j => j.Branch).WithMany().HasForeignKey(j => j.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobOrder>()
            .HasOne(j => j.BillingClient).WithMany().HasForeignKey(j => j.BillingClientId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<CompanyBranch>().HasIndex(c => c.BranchCode).IsUnique();
        mb.Entity<ShipmentActivity>().HasIndex(s => s.ActivityCode).IsUnique();
        mb.Entity<JobOrder>()
            .HasOne(j => j.Shipper).WithMany().HasForeignKey(j => j.ShipperId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<JobOrder>()
            .HasOne(j => j.Consignee).WithMany().HasForeignKey(j => j.ConsigneeId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<JobOrder>()
            .HasOne(j => j.SaleStaff).WithMany().HasForeignKey(j => j.SaleStaffId)
            .OnDelete(DeleteBehavior.SetNull);
        // NOTE: Both Port FKs use Restrict (NoAction) to avoid SQL Server's
        // "multiple cascade paths" error — two SetNull paths to the same table
        // are rejected. Effect: a Port can't be deleted while a JobOrder uses it.
        mb.Entity<JobOrder>()
            .HasOne(j => j.LoadPort).WithMany().HasForeignKey(j => j.LoadPortId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<JobOrder>()
            .HasOne(j => j.DischargePort).WithMany().HasForeignKey(j => j.DischargePortId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<JobOrder>()
            .HasOne(j => j.Commodity).WithMany().HasForeignKey(j => j.CommodityId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobOrder>()
            .HasOne(j => j.ContainerSize).WithMany().HasForeignKey(j => j.ContainerSizeId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobOrder>()
            .HasOne(j => j.Currency).WithMany().HasForeignKey(j => j.CurrencyId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<JobOrder>().HasIndex(j => j.JobOrderNo).IsUnique();
        mb.Entity<JobOrder>().HasIndex(j => new { j.Mode, j.FinYear });
        mb.Entity<JobOrder>().HasIndex(j => j.Status);

        mb.Entity<JobOrderEvent>()
            .HasOne(e => e.JobOrder).WithMany(j => j.Events).HasForeignKey(e => e.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<JobOrderEvent>().HasIndex(e => new { e.JobOrderId, e.At });
    }
}
