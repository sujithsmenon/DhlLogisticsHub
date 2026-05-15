namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

public class ExportJobService
{
    private readonly AppDbContext _db;

    public ExportJobService(AppDbContext db) => _db = db;

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<ExportJob>> GetAllAsync() =>
        await _db.ExportJobs
            .Include(j => j.Transporter)
            .Include(j => j.Events)
            .OrderByDescending(j => j.ReceivedAt)
            .ToListAsync();

    public async Task<ExportJob?> GetAsync(int id) =>
        await _db.ExportJobs
            .Include(j => j.Transporter)
            .Include(j => j.Events)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<List<Transporter>> GetTransportersAsync() =>
        await _db.Transporters
            .Where(t => t.IsActive)
            .OrderBy(t => t.CompanyName)
            .ToListAsync();

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ExportJob> CreateAsync(ExportJob job)
    {
        job.Id         = 0;
        job.ReceivedAt = DateTime.UtcNow;
        job.Status     = ExportJobStatus.Received;
        _db.ExportJobs.Add(job);
        AddEvent(job, "Created", $"Export job created. Customer: {job.CustomerName}. Ref: {job.JobReference}.");
        await _db.SaveChangesAsync();
        return job;
    }

    // ── Workflow actions ──────────────────────────────────────────────────────

    public async Task InitiatePickupAsync(int id)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.PickupInitiatedAt = DateTime.UtcNow;
        job.Status            = ExportJobStatus.PickupInitiated;
        AddEvent(job, "PickupInitiated",
            $"Pickup formally initiated with {(job.IsEmergency ? "Emergency" : "Standard")} priority.");
        await _db.SaveChangesAsync();
    }

    public async Task BookTransporterAsync(int id, int transporterId)
    {
        var job         = await _db.ExportJobs.FindAsync(id);
        var transporter = await _db.Transporters.FindAsync(transporterId);
        if (job is null) return;

        job.TransporterId      = transporterId;
        job.TransporterBookedAt = DateTime.UtcNow;
        job.Status             = ExportJobStatus.TransporterBooked;
        AddEvent(job, "TransporterBooked",
            $"Transporter contacted: {transporter?.CompanyName}. Vehicle dispatch requested.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordVehicleAsync(int id, string vehicleNumber, string driverName, string driverMobile)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.VehicleNumber    = vehicleNumber;
        job.DriverName       = driverName;
        job.DriverMobile     = driverMobile;
        job.VehicleAssignedAt = DateTime.UtcNow;
        job.Status           = ExportJobStatus.VehicleAssigned;
        AddEvent(job, "VehicleAssigned",
            $"Vehicle: {vehicleNumber} | Driver: {driverName} | Mobile: {driverMobile}");
        await _db.SaveChangesAsync();
    }

    public async Task RecordCargoCollectedAsync(int id, string riNumber)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.RiNumber         = riNumber;
        job.CargoCollectedAt = DateTime.UtcNow;
        job.Status           = ExportJobStatus.CargoCollected;
        AddEvent(job, "CargoCollected",
            $"Cargo collected by transporter. RI submitted to Cochin Port Authority. RI No: {riNumber}");
        await _db.SaveChangesAsync();
    }

    public async Task GenerateChecklistAsync(int id, string checklistRef)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.ChecklistRef  = checklistRef;
        job.ChecklistSentAt = DateTime.UtcNow;
        job.Status        = ExportJobStatus.ChecklistPending;
        AddEvent(job, "ChecklistGenerated",
            $"Cargo checklist generated via Focus Software (Ref: {checklistRef}) and shared with customer. Awaiting customer confirmation.");
        await _db.SaveChangesAsync();
    }

    public async Task ConfirmChecklistAsync(int id)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.CustomerConfirmedAt = DateTime.UtcNow;
        job.Status              = ExportJobStatus.ChecklistConfirmed;
        AddEvent(job, "ChecklistConfirmed", "Customer has formally confirmed the checklist. Proceeding to ICEGATE submission.");
        await _db.SaveChangesAsync();
    }

    public async Task SubmitIcegateAsync(int id, string icegateRef, string shippingBillNumber)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.IcegateRef         = icegateRef;
        job.ShippingBillNumber = shippingBillNumber;
        job.ShippingBillAt     = DateTime.UtcNow;
        job.Status             = ExportJobStatus.IcegateSubmitted;
        AddEvent(job, "IcegateSubmitted",
            $"Job submitted to ICEGATE with all supporting documents. Shipping Bill No: {shippingBillNumber} (ICEGATE Ref: {icegateRef}).");
        await _db.SaveChangesAsync();
    }

    public async Task HandOverToOpsAsync(int id, string operationStaffName)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.OperationStaffName = operationStaffName;
        job.HandedOverAt       = DateTime.UtcNow;
        job.Status             = ExportJobStatus.HandedToOps;
        AddEvent(job, "HandedToOps",
            $"Checklist and SB No. {job.ShippingBillNumber} handed over to Operation Staff: {operationStaffName}.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordPortClearedAsync(int id, DateTime? arrivedAt)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.PortArrivedAt = arrivedAt ?? DateTime.UtcNow;
        job.PortClearedAt = DateTime.UtcNow;
        job.Status        = ExportJobStatus.PortCleared;
        AddEvent(job, "PortCleared",
            $"Cargo arrived at Cochin Port and all customs & terminal clearances completed.");
        await _db.SaveChangesAsync();
    }

    public async Task MarkExportReadyAsync(int id)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.ExportReadyAt = DateTime.UtcNow;
        job.Status        = ExportJobStatus.ExportReady;
        AddEvent(job, "ExportReady", "Final export clearance received. Cargo is ready for vessel loading.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordBookingAsync(int id, string bookingReference)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.BookingReference = bookingReference;
        job.BookingReceivedAt = DateTime.UtcNow;
        job.Status            = ExportJobStatus.BookingReceived;
        AddEvent(job, "BookingReceived",
            $"Official booking confirmation received. Booking Ref: {bookingReference}.");
        await _db.SaveChangesAsync();
    }

    public async Task ForwardToSurveyAsync(int id, string surveyTeamName)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.SurveyTeamName    = surveyTeamName;
        job.SurveyForwardedAt = DateTime.UtcNow;
        job.Status            = ExportJobStatus.SurveyInProgress;
        AddEvent(job, "SurveyForwarded",
            $"Booking details forwarded to Survey Team: {surveyTeamName}. Container inspection in progress.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordLoadingAuthAsync(int id, string authReference)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.LoadingAuthReference = authReference;
        job.LoadingAuthAt        = DateTime.UtcNow;
        job.Status               = ExportJobStatus.LoadingAuthorized;
        AddEvent(job, "LoadingAuthorized",
            $"Permission granted by Customs and Cochin Port Authority to begin stuffing. Auth Ref: {authReference}.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordCargoLoadedAsync(int id, string containerNumber, string sealNumber)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.ContainerNumber = containerNumber;
        job.SealNumber      = sealNumber;
        job.CargoLoadedAt   = DateTime.UtcNow;
        job.Status          = ExportJobStatus.CargoLoaded;
        AddEvent(job, "CargoLoaded",
            $"Cargo loaded into container {containerNumber} (Seal: {sealNumber}). Loading process complete.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordSiDoAsync(int id, string siRef, string doRef, string thirdPartyName)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.ShippingInstruction = siRef;
        job.DeliveryOrderRef    = doRef;
        job.ThirdPartyName      = thirdPartyName;
        job.SiDoReceivedAt      = DateTime.UtcNow;
        job.Status              = ExportJobStatus.SiDoReceived;
        AddEvent(job, "SiDoReceived",
            $"SI (Ref: {siRef}) and DO (Ref: {doRef}) received from DHL and shared with {thirdPartyName} for SEZ-4 documentation.");
        await _db.SaveChangesAsync();
    }

    public async Task MarkInTransitToIcttAsync(int id)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.InTransitToIcttAt = DateTime.UtcNow;
        job.Status            = ExportJobStatus.InTransitToIctt;
        AddEvent(job, "InTransitToIctt",
            "Loaded container dispatched to ICTT Vallarpadam for terminal gate-in.");
        await _db.SaveChangesAsync();
    }

    public async Task RecordTerminalGateInAsync(int id, string sez4Reference, string vesselName, string voyageNumber)
    {
        var job = await _db.ExportJobs.FindAsync(id);
        if (job is null) return;

        job.Sez4Reference    = sez4Reference;
        job.VesselName       = vesselName;
        job.VoyageNumber     = voyageNumber;
        job.TerminalGateInAt = DateTime.UtcNow;
        job.Status           = ExportJobStatus.TerminalGateIn;
        AddEvent(job, "TerminalGateIn",
            $"SEZ-4 issued (Ref: {sez4Reference}). Container gated in at ICTT Vallarpadam. Vessel: {vesselName} / Voyage: {voyageNumber}. Awaiting vessel loading.");
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AddEvent(ExportJob job, string type, string desc, string? file = null)
    {
        job.Events.Add(new ExportJobEvent
        {
            EventType   = type,
            Description = desc,
            FilePath    = file,
            CreatedAt   = DateTime.UtcNow,
        });
    }
}
