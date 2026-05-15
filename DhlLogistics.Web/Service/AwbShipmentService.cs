namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

public class AwbShipmentService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;

    public AwbShipmentService(AppDbContext db, NotificationService notify)
    {
        _db     = db;
        _notify = notify;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<AwbShipment>> GetAllAsync() =>
        await _db.AwbShipments
            .Include(a => a.Transporter)
            .Include(a => a.Events)
            .OrderByDescending(a => a.ReceivedAt)
            .ToListAsync();

    public async Task<AwbShipment?> GetAsync(int id) =>
        await _db.AwbShipments
            .Include(a => a.Transporter)
            .Include(a => a.Events)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<List<Transporter>> GetTransportersAsync() =>
        await _db.Transporters
            .Where(t => t.IsActive)
            .OrderBy(t => t.CompanyName)
            .ToListAsync();

    public async Task<List<Transporter>> GetAllTransportersAsync() =>
        await _db.Transporters
            .OrderBy(t => t.CompanyName)
            .ToListAsync();

    // ── Transporter CRUD ──────────────────────────────────────────────────────

    public async Task AddTransporterAsync(Transporter t)
    {
        t.Id = 0;
        _db.Transporters.Add(t);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateTransporterAsync(Transporter t)
    {
        _db.Entry(t).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTransporterAsync(int id)
    {
        var e = await _db.Transporters.FindAsync(id);
        if (e is not null) { _db.Transporters.Remove(e); await _db.SaveChangesAsync(); }
    }

    // ── Manual AWB entry / edit ───────────────────────────────────────────────

    public async Task<AwbShipment> CreateManualAsync(AwbShipment awb)
    {
        awb.Id         = 0;
        awb.ReceivedAt = DateTime.UtcNow;
        awb.Status     = AwbStatus.Received;
        _db.AwbShipments.Add(awb);
        await _db.SaveChangesAsync();
        return awb;
    }

    public async Task UpdateAwbDetailsAsync(AwbShipment patch)
    {
        var existing = await _db.AwbShipments.FindAsync(patch.Id);
        if (existing is null) return;

        existing.HawbNo               = patch.HawbNo;
        existing.IssuedDate           = patch.IssuedDate;
        existing.StationCode          = patch.StationCode;
        existing.ShipperAccount       = patch.ShipperAccount;
        existing.ShipperName          = patch.ShipperName;
        existing.ShipperAddress       = patch.ShipperAddress;
        existing.ShipperPhone         = patch.ShipperPhone;
        existing.ShipperContact       = patch.ShipperContact;
        existing.ConsigneeAccount     = patch.ConsigneeAccount;
        existing.ConsigneeName        = patch.ConsigneeName;
        existing.ConsigneeAddress     = patch.ConsigneeAddress;
        existing.ConsigneePhone       = patch.ConsigneePhone;
        existing.ConsigneeContact     = patch.ConsigneeContact;
        existing.OriginStation        = patch.OriginStation;
        existing.DestinationStation   = patch.DestinationStation;
        existing.ReferenceNumbers     = patch.ReferenceNumbers;
        existing.HandlingInfo         = patch.HandlingInfo;
        existing.Pieces               = patch.Pieces;
        existing.GrossWeightKg        = patch.GrossWeightKg;
        existing.ChargeableWeight     = patch.ChargeableWeight;
        existing.RateClass            = patch.RateClass;
        existing.GoodsDescription     = patch.GoodsDescription;
        existing.HsCode               = patch.HsCode;
        existing.Dimensions           = patch.Dimensions;
        existing.VolumeCbm            = patch.VolumeCbm;
        existing.Slac                 = patch.Slac;
        existing.Currency             = patch.Currency;
        existing.DeclaredValueCarriage = patch.DeclaredValueCarriage;
        existing.DeclaredValueCustoms  = patch.DeclaredValueCustoms;

        await _db.SaveChangesAsync();
    }

    // ── Workflow actions ──────────────────────────────────────────────────────

    public async Task AssignTransporterAsync(int awbId, int transporterId,
        string pickupLocation, string dropLocation)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        var transporter = await _db.Transporters.FindAsync(transporterId);

        awb.TransporterId          = transporterId;
        awb.PickupLocation         = pickupLocation;
        awb.DropLocation           = dropLocation;
        awb.TransporterAssignedAt  = DateTime.UtcNow;
        awb.Status                 = AwbStatus.TransporterAssigned;

        AddEvent(awb, "TransporterAssigned",
            $"Transporter assigned: {transporter?.CompanyName}. Pickup: {pickupLocation}. Drop: {dropLocation}.");

        await _db.SaveChangesAsync();
    }

    public async Task RecordVehicleDetailsAsync(int awbId,
        string vehicleNumber, string driverName, string driverMobile)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.VehicleNumber    = vehicleNumber;
        awb.DriverName       = driverName;
        awb.DriverMobile     = driverMobile;
        awb.VehicleDetailsAt = DateTime.UtcNow;
        awb.Status           = AwbStatus.VehicleAssigned;

        AddEvent(awb, "VehicleDetails",
            $"Vehicle: {vehicleNumber} | Driver: {driverName} | Mobile: {driverMobile}");

        await _db.SaveChangesAsync();
    }

    public async Task MarkInTransitAsync(int awbId)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.Status = AwbStatus.InTransit;
        AddEvent(awb, "InTransit", "Shipment is in transit to Cochin Customs Port.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordPortDeliveryAsync(int awbId,
        string? deliveryPhotoPath, string? godownReceiptPath)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.DeliveredAtPortAt = DateTime.UtcNow;
        awb.DeliveryPhotoPath = deliveryPhotoPath;
        awb.GodownReceiptPath = godownReceiptPath;
        awb.Status            = AwbStatus.DeliveredAtPort;

        AddEvent(awb, "DeliveryAtPort",
            "Package delivered at Cochin Customs Port. Photo and godown receipt uploaded.",
            deliveryPhotoPath ?? godownReceiptPath);

        // Customs papers expected within 2 days
        awb.Status = AwbStatus.CustomsPending;
        AddEvent(awb, "CustomsPending", "Awaiting customs clearance papers (expected within 2 days).");

        await _db.SaveChangesAsync();
    }

    public async Task RecordCustomsDocsAsync(int awbId, string? customsDocPath)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.CustomsDocsReceivedAt = DateTime.UtcNow;
        awb.CustomsDocPath        = customsDocPath;
        awb.Status                = AwbStatus.CustomsCleared;

        AddEvent(awb, "CustomsCleared", "Customs papers received and recorded.", customsDocPath);
        await _db.SaveChangesAsync();
    }

    public async Task RecordInvoiceSentAsync(int awbId, string invoiceNumber, string? invoiceFilePath)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.InvoiceSentAt    = DateTime.UtcNow;
        awb.InvoiceNumber    = invoiceNumber;
        awb.InvoiceFilePath  = invoiceFilePath;
        awb.Status           = AwbStatus.InvoiceSent;

        AddEvent(awb, "InvoiceSent",
            $"Invoice #{invoiceNumber} sent to DHL.", invoiceFilePath);

        await _db.SaveChangesAsync();
    }

    public async Task MarkCompletedAsync(int awbId)
    {
        var awb = await _db.AwbShipments.FindAsync(awbId);
        if (awb is null) return;

        awb.Status = AwbStatus.Completed;
        AddEvent(awb, "Completed", "Shipment workflow completed.");
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AddEvent(AwbShipment awb, string type, string desc, string? file = null)
    {
        awb.Events.Add(new ShipmentEvent
        {
            EventType   = type,
            Description = desc,
            FilePath    = file,
            CreatedAt   = DateTime.UtcNow,
        });
    }
}
