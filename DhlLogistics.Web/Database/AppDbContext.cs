namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    // ── CBM User Management (profile + activity/branch scoping) ───────────────
    public DbSet<RegisterdUser> RegisterdUsers => Set<RegisterdUser>();
    public DbSet<UserShipmentActivityPermission> UserShipmentActivityPermissions => Set<UserShipmentActivityPermission>();
    public DbSet<UserCompanyBranchPermission>    UserCompanyBranchPermissions    => Set<UserCompanyBranchPermission>();

    // ── M4 Job Orders ────────────────────────────────────────────────────────
    public DbSet<JobOrder>         JobOrders         => Set<JobOrder>();
    public DbSet<JobOrderEvent>    JobOrderEvents    => Set<JobOrderEvent>();
    public DbSet<JobOrderOperation> JobOrderOperations => Set<JobOrderOperation>();
    public DbSet<JobCharge>         JobCharges         => Set<JobCharge>();
    // Job Operations foundation (new standalone tracking entity; does not alter existing JobOrder tables)
    public DbSet<JobOperation>      JobOperations      => Set<JobOperation>();
    public DbSet<CompanyBranch>    CompanyBranches   => Set<CompanyBranch>();
    public DbSet<ShipmentActivity> ShipmentActivities => Set<ShipmentActivity>();
    // Issuer company details for invoice branding (replaces InvoiceService hard-coded constants)
    public DbSet<CompanyDetails>   CompanyDetails    => Set<CompanyDetails>();

    // ── M4 Billing ───────────────────────────────────────────────────────────
    public DbSet<Bill>            Bills            => Set<Bill>();
    public DbSet<BillCharge>      BillCharges      => Set<BillCharge>();
    public DbSet<BillEvent>       BillEvents       => Set<BillEvent>();
    public DbSet<InvoiceDocument> InvoiceDocuments => Set<InvoiceDocument>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();

    // ── M4 Accounts ──────────────────────────────────────────────────────────
    public DbSet<AccountHead>   AccountHeads   => Set<AccountHead>();
    public DbSet<Voucher>       Vouchers       => Set<Voucher>();
    public DbSet<VoucherLine>   VoucherLines   => Set<VoucherLine>();
    public DbSet<VoucherEvent>  VoucherEvents  => Set<VoucherEvent>();

    // ── Navigation menu (consolidated into Postgres so it works on AWS; was a
    //    separate local SQL Server store via the now-removed MenuDbContext) ──────
    public DbSet<Menu> Menus => Set<Menu>();

    // ── Workflow Engine (cross-cutting activity + audit log) ───────────────────
    public DbSet<WorkflowAuditLog> WorkflowAuditLogs => Set<WorkflowAuditLog>();

    // ── Universal search audit trail ───────────────────────────────────────────
    public DbSet<SearchAuditLog> SearchAuditLogs => Set<SearchAuditLog>();

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

        // ── CBM User Management ──────────────────────────────────────────────
        // RegisterdUser: surrogate PK UserId, one profile per AspNetUsers row.
        mb.Entity<RegisterdUser>(e =>
        {
            e.HasKey(r => r.UserId);
            e.HasIndex(r => r.AspNetUserId).IsUnique();
            e.HasOne(r => r.Staff).WithMany().HasForeignKey(r => r.StaffId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<UserShipmentActivityPermission>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ActivityId }).IsUnique();
            e.HasOne<RegisterdUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ShipmentActivity>().WithMany().HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<UserCompanyBranchPermission>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.BranchId }).IsUnique();
            e.HasOne<RegisterdUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CompanyBranch>().WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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
        // Customer invoice reference: mandatory, non-unique (many jobs share one), indexed for search.
        mb.Entity<JobOrder>().Property(j => j.CustomerInvoiceNumber).HasMaxLength(100).IsRequired();
        mb.Entity<JobOrder>().HasIndex(j => j.CustomerInvoiceNumber);

        mb.Entity<JobOrderEvent>()
            .HasOne(e => e.JobOrder).WithMany(j => j.Events).HasForeignKey(e => e.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<JobOrderEvent>().HasIndex(e => new { e.JobOrderId, e.At });

        // JobOrder operations sub-grid (business operations within a job)
        mb.Entity<JobOrderOperation>().Property(o => o.Cost).HasPrecision(18, 2);
        mb.Entity<JobOrderOperation>()
            .HasOne(o => o.JobOrder).WithMany(j => j.Operations).HasForeignKey(o => o.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<JobOrderOperation>()
            .HasOne(o => o.OperatedByClient).WithMany().HasForeignKey(o => o.OperatedByClientId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobOrderOperation>()
            .HasOne(o => o.HandledByStaff).WithMany().HasForeignKey(o => o.HandledByStaffId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobOrderOperation>().HasIndex(o => o.JobOrderId);

        // JobOrder sale charge lines (customer-facing; copied into the bill on approval).
        mb.Entity<JobOrder>().Property(j => j.SubTotal).HasPrecision(18, 2);
        mb.Entity<JobOrder>().Property(j => j.GstTotal).HasPrecision(18, 2);
        mb.Entity<JobOrder>().Property(j => j.TotalAmount).HasPrecision(18, 2);

        mb.Entity<JobCharge>().Property(c => c.Quantity).HasPrecision(12, 3);
        mb.Entity<JobCharge>().Property(c => c.Rate).HasPrecision(18, 4);
        mb.Entity<JobCharge>().Property(c => c.Amount).HasPrecision(18, 2);
        mb.Entity<JobCharge>().Property(c => c.GstRate).HasPrecision(5, 2);
        mb.Entity<JobCharge>().Property(c => c.GstAmount).HasPrecision(18, 2);
        mb.Entity<JobCharge>().Property(c => c.NetAmount).HasPrecision(18, 2);

        mb.Entity<JobCharge>()
            .HasOne(c => c.JobOrder).WithMany(j => j.Charges).HasForeignKey(c => c.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<JobCharge>()
            .HasOne(c => c.ChargeCode).WithMany().HasForeignKey(c => c.ChargeCodeId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobCharge>()
            .HasOne(c => c.Sac).WithMany().HasForeignKey(c => c.SacId)
            .OnDelete(DeleteBehavior.SetNull);
        // Optional owning operation — deleting an operation just unlinks its charges (keeps the lines).
        mb.Entity<JobCharge>()
            .HasOne(c => c.JobOperation).WithMany().HasForeignKey(c => c.JobOperationId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<JobCharge>().HasIndex(c => c.JobOrderId);
        mb.Entity<JobCharge>().HasIndex(c => c.JobOperationId);

        // ── Job Operations foundation (new standalone tracking table) ───────────
        mb.ApplyConfiguration(new Configurations.JobOperationConfiguration());

        // ── Company Details (invoice-issuer branding master) ────────────────────
        mb.ApplyConfiguration(new Configurations.CompanyDetailsConfiguration());

        // ── M4 Billing ────────────────────────────────────────────────────────
        mb.Entity<Bill>().Property(b => b.ExchangeRate).HasPrecision(18, 6);
        mb.Entity<Bill>().Property(b => b.SubTotal).HasPrecision(18, 2);
        mb.Entity<Bill>().Property(b => b.GstAmount).HasPrecision(18, 2);
        mb.Entity<Bill>().Property(b => b.TotalAmount).HasPrecision(18, 2);

        mb.Entity<Bill>()
            .HasOne(b => b.Branch).WithMany().HasForeignKey(b => b.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Bill>()
            .HasOne(b => b.JobOrder).WithMany().HasForeignKey(b => b.JobOrderId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Bill>()
            .HasOne(b => b.BillingClient).WithMany().HasForeignKey(b => b.BillingClientId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Bill>()
            .HasOne(b => b.Currency).WithMany().HasForeignKey(b => b.CurrencyId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Bill>().HasIndex(b => b.BillNo).IsUnique();
        mb.Entity<Bill>().HasIndex(b => new { b.Mode, b.FinYear });
        mb.Entity<Bill>().HasIndex(b => b.Status);
        // Customer invoice reference copied from the source job — indexed for search on the bill lists.
        mb.Entity<Bill>().Property(b => b.CustomerInvoiceNumber).HasMaxLength(100);
        mb.Entity<Bill>().HasIndex(b => b.CustomerInvoiceNumber);

        // Generic billing source (JobOrder / AWB / Export) + transport snapshot. All nullable; indexed so a
        // source's bills can be looked up. Transporter is an optional FK (SetNull keeps the bill on delete).
        mb.Entity<Bill>().Property(b => b.Quantity).HasPrecision(18, 3);
        mb.Entity<Bill>().Property(b => b.WeightKg).HasPrecision(18, 3);
        mb.Entity<Bill>().Property(b => b.VolumeCbm).HasPrecision(18, 3);
        mb.Entity<Bill>().HasIndex(b => new { b.SourceType, b.SourceId });
        mb.Entity<Bill>()
            .HasOne(b => b.Transporter).WithMany().HasForeignKey(b => b.TransporterId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<BillCharge>().Property(c => c.Quantity).HasPrecision(12, 3);
        mb.Entity<BillCharge>().Property(c => c.Rate).HasPrecision(18, 4);
        mb.Entity<BillCharge>().Property(c => c.Amount).HasPrecision(18, 2);
        mb.Entity<BillCharge>().Property(c => c.GstRate).HasPrecision(5, 2);
        mb.Entity<BillCharge>().Property(c => c.GstAmount).HasPrecision(18, 2);
        mb.Entity<BillCharge>().Property(c => c.NetAmount).HasPrecision(18, 2);

        mb.Entity<BillCharge>()
            .HasOne(c => c.Bill).WithMany(b => b.Charges).HasForeignKey(c => c.BillId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<BillCharge>()
            .HasOne(c => c.ChargeCode).WithMany().HasForeignKey(c => c.ChargeCodeId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<BillCharge>()
            .HasOne(c => c.Sac).WithMany().HasForeignKey(c => c.SacId)
            .OnDelete(DeleteBehavior.SetNull);
        // Plain reference back to the source operation (no FK — an issued bill stays stable). Indexed for grouping.
        mb.Entity<BillCharge>().HasIndex(c => c.JobOperationId);

        mb.Entity<BillEvent>()
            .HasOne(e => e.Bill).WithMany(b => b.Events).HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<BillEvent>().HasIndex(e => new { e.BillId, e.At });

        // ── Invoice documents (customer-invoice PDF + uploaded vendor/credit/debit docs) ──
        mb.Entity<InvoiceDocument>()
            .HasOne(d => d.Bill).WithMany(b => b.InvoiceDocuments).HasForeignKey(d => d.BillId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<InvoiceDocument>().HasIndex(d => new { d.BillId, d.IsActive });

        // ── Consolidated customer invoice (Billing Group = bills sharing a CustomerInvoiceNumber) ──
        // Own number sequence (CI/FY/NNNN) per FY, mirroring the Bill numbering rule.
        mb.Entity<CustomerInvoice>().HasIndex(i => i.InvoiceNo).IsUnique();
        mb.Entity<CustomerInvoice>().HasIndex(i => new { i.FinYear });
        mb.Entity<CustomerInvoice>().Property(i => i.CustomerInvoiceNumber).HasMaxLength(100).IsRequired();
        // The Billing Group key — indexed, since every group lookup and search goes through it.
        mb.Entity<CustomerInvoice>().HasIndex(i => i.CustomerInvoiceNumber);
        mb.Entity<CustomerInvoice>().Property(i => i.SubTotal).HasPrecision(18, 2);
        mb.Entity<CustomerInvoice>().Property(i => i.GstAmount).HasPrecision(18, 2);
        mb.Entity<CustomerInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
        mb.Entity<CustomerInvoice>()
            .HasOne(i => i.BillingClient).WithMany().HasForeignKey(i => i.BillingClientId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<CustomerInvoice>()
            .HasOne(i => i.Branch).WithMany().HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<CustomerInvoice>()
            .HasOne(i => i.Currency).WithMany().HasForeignKey(i => i.CurrencyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Bill → consolidated invoice. SetNull (not Cascade): cancelling/deleting an invoice must release
        // its bills back to un-invoiced, never delete the bills or their accounting.
        mb.Entity<Bill>()
            .HasOne(b => b.CustomerInvoice).WithMany(i => i.Bills).HasForeignKey(b => b.CustomerInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Bill>().HasIndex(b => b.CustomerInvoiceId);

        // Consolidated-invoice PDF reuses the InvoiceDocument store (BillId still points at the anchor bill,
        // so every existing by-BillId query is unaffected).
        mb.Entity<InvoiceDocument>()
            .HasOne(d => d.CustomerInvoice).WithMany(i => i.Documents).HasForeignKey(d => d.CustomerInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<InvoiceDocument>().HasIndex(d => new { d.CustomerInvoiceId, d.IsActive });

        // ── Workflow Engine audit/activity log (no hard FK — survives entity deletion) ──
        mb.Entity<WorkflowAuditLog>().HasIndex(l => new { l.Kind, l.At });
        mb.Entity<WorkflowAuditLog>().HasIndex(l => new { l.EntityType, l.EntityId });

        // ── Universal search audit + indexes on the most-searched code/number columns ──────────
        mb.Entity<SearchAuditLog>().HasIndex(l => l.At);
        // (JobOrderNo / BillNo / VoucherNo already carry unique indexes; add the remaining hot lookups.)
        mb.Entity<Bill>().HasIndex(b => b.InvoiceNumber);
        mb.Entity<AwbShipment>().HasIndex(a => a.HawbNo);
        mb.Entity<ExportJob>().HasIndex(e => e.JobReference);

        // Billing Group key on the two remaining operational modules, so all four (Clearance, Forwarding,
        // Export, AWB) share one business reference. Same shape as Bill/JobOrder: 100 chars, indexed
        // (every group lookup and search goes through it). Nullable — legacy rows simply form no group.
        mb.Entity<AwbShipment>().Property(a => a.CustomerInvoiceNumber).HasMaxLength(100);
        mb.Entity<AwbShipment>().HasIndex(a => a.CustomerInvoiceNumber);
        mb.Entity<ExportJob>().Property(e => e.CustomerInvoiceNumber).HasMaxLength(100);
        mb.Entity<ExportJob>().HasIndex(e => e.CustomerInvoiceNumber);
        mb.Entity<Container>().HasIndex(c => c.ContainerNumber);
        mb.Entity<Vehicle>().HasIndex(v => v.PlateNumber);

        // ── M4 Accounts ───────────────────────────────────────────────────────
        mb.Entity<AccountHead>().Property(a => a.OpeningBalance).HasPrecision(18, 2);
        mb.Entity<AccountHead>().HasIndex(a => a.AccountCode).IsUnique();
        mb.Entity<AccountHead>().HasIndex(a => a.AccountName);

        mb.Entity<Voucher>().Property(v => v.TotalDebit).HasPrecision(18, 2);
        mb.Entity<Voucher>().Property(v => v.TotalCredit).HasPrecision(18, 2);

        mb.Entity<Voucher>()
            .HasOne(v => v.Branch).WithMany().HasForeignKey(v => v.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Voucher>()
            .HasOne(v => v.CashOrBankAccount).WithMany().HasForeignKey(v => v.CashOrBankAccountId)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Voucher>()
            .HasOne(v => v.Party).WithMany().HasForeignKey(v => v.PartyId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Voucher>().HasIndex(v => v.VoucherNo).IsUnique();
        mb.Entity<Voucher>().HasIndex(v => new { v.Type, v.FinYear });
        mb.Entity<Voucher>().HasIndex(v => v.Status);

        mb.Entity<VoucherLine>().Property(l => l.Amount).HasPrecision(18, 2);
        mb.Entity<VoucherLine>()
            .HasOne(l => l.Voucher).WithMany(v => v.Lines).HasForeignKey(l => l.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<VoucherLine>()
            .HasOne(l => l.AccountHead).WithMany().HasForeignKey(l => l.AccountHeadId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<VoucherEvent>()
            .HasOne(e => e.Voucher).WithMany(v => v.Events).HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<VoucherEvent>().HasIndex(e => new { e.VoucherId, e.At });

        // ── Navigation menu (Postgres) ───────────────────────────────────────
        mb.Entity<Menu>(e =>
        {
            e.ToTable("Menus");
            e.HasKey(m => m.MenuId);
            e.Property(m => m.MenuName).HasMaxLength(100).IsRequired();
            e.Property(m => m.Icon).HasMaxLength(16);
            e.Property(m => m.PageName).HasMaxLength(200);
            e.HasIndex(m => m.ParentId);
            e.HasIndex(m => m.ShowOrder);
        });

        // ── DateTime → UTC value converters ──────────────────────────────────
        // Npgsql maps DateTime to 'timestamp with time zone', which only accepts
        // Kind=Utc. Values from Syncfusion date/time pickers are Local/Unspecified and
        // would throw "Cannot write DateTime with Kind=Local...". A SaveChanges-time
        // relabel does NOT work: EF's DateTime value comparer ignores Kind, so changing
        // CurrentValue to the same ticks is treated as "no change" and skipped.
        // A value converter runs at the provider boundary (after change tracking) so it
        // always applies. SpecifyKind (not ToUniversalTime) preserves the picked
        // wall-clock value; reads relabel the incoming value as Utc.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in mb.Model.GetEntityTypes())
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utcNullableConverter);
            }
    }

}
