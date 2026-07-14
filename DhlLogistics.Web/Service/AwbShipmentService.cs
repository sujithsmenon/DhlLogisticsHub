namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Workflow;
using DhlLogistics.Web.Workflow.Handlers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

public class AwbShipmentService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly AuthenticationStateProvider _authProvider;
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly AwbWorkflowHandler _handler;

    public AwbShipmentService(AppDbContext db, NotificationService notify,
                              AuthenticationStateProvider authProvider,
                              WorkflowOrchestrator orchestrator, AwbWorkflowHandler handler)
    {
        _db           = db;
        _notify       = notify;
        _authProvider = authProvider;
        _orchestrator = orchestrator;
        _handler      = handler;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
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

    // Manual create + details edit routed through the Workflow Engine (validate → persist →
    // timeline → activity → audit → commit → dashboard/search/notify). The field-level patch and
    // the create defaults live in AwbWorkflowHandler.
    public async Task<AwbShipment> CreateManualAsync(AwbShipment awb)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Create, await CurrentUserAsync(), awb, _handler);
        await _orchestrator.RunAsync(ctx);
        return awb;
    }

    public async Task UpdateAwbDetailsAsync(AwbShipment patch)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Update, await CurrentUserAsync(), patch, _handler);
        await _orchestrator.RunAsync(ctx);
    }

    /// <summary>
    /// Sets/updates the customer's invoice reference — the Billing Group key — on an existing AWB shipment.
    /// Needed because AWB has no edit screen: without this, a shipment created before the field existed (or
    /// left blank) could never join a Billing Group. Scoped to this one field rather than going through
    /// UpdateAwbDetailsAsync, so setting a reference cannot disturb the stage workflow.
    ///
    /// NOTE: this is the CUSTOMER's reference, not <see cref="AwbShipment.InvoiceNumber"/> (the Stage-5
    /// invoice we raise TO DHL) — two unrelated documents.
    /// </summary>
    public async Task SetCustomerInvoiceNumberAsync(int id, string? customerInvoiceNumber)
    {
        var awb = await _db.AwbShipments.FindAsync(id);
        if (awb is null) return;

        var value = string.IsNullOrWhiteSpace(customerInvoiceNumber) ? null : customerInvoiceNumber.Trim();
        if (awb.CustomerInvoiceNumber == value) return;

        awb.CustomerInvoiceNumber = value;
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
