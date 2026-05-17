namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Aggregates AwbShipment + ExportJob + JobOrder into the unified ActivityReport
/// shape for the admin Daily/Weekly/Monthly dashboard. Items are bucketed into
/// 4 quadrants (Sea/Air × Import/Export) and 5 canonical stages (Received →
/// InTransit → AtPort → Cleared → Completed).
///
/// Quadrant source mapping:
///   Air Import → AwbShipment (always)  + JobOrder (Air & Import)
///   Air Export → JobOrder (Air & Export)
///   Sea Import → JobOrder (Sea & Import)
///   Sea Export → ExportJob (always)    + JobOrder (Sea & Export)
/// </summary>
public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    public Task<ActivityReport> GetDailyAsync()
    {
        var to   = DateTime.UtcNow;
        var from = to.Date;                       // start of today UTC
        return BuildAsync(from, to, "Daily");
    }

    public Task<ActivityReport> GetWeeklyAsync()
    {
        var to   = DateTime.UtcNow;
        var from = to.Date.AddDays(-6);           // rolling 7 days inc. today
        return BuildAsync(from, to, "Weekly");
    }

    public Task<ActivityReport> GetMonthlyAsync()
    {
        var to   = DateTime.UtcNow;
        var from = to.Date.AddDays(-29);          // rolling 30 days inc. today
        return BuildAsync(from, to, "Monthly");
    }

    public Task<ActivityReport> GetRangeAsync(DateTime from, DateTime to) =>
        BuildAsync(from, to, $"{from:dd MMM} → {to:dd MMM}");

    // ── Core builder ─────────────────────────────────────────────────────────

    private async Task<ActivityReport> BuildAsync(DateTime from, DateTime to, string label)
    {
        // Pull each source in parallel (3 cheap reads against indexed date cols).
        var awbsTask = _db.AwbShipments
            .AsNoTracking()
            .Where(a => a.ReceivedAt >= from && a.ReceivedAt <= to)
            .ToListAsync();

        var exportsTask = _db.ExportJobs
            .AsNoTracking()
            .Where(e => e.ReceivedAt >= from && e.ReceivedAt <= to)
            .ToListAsync();

        var jobsTask = _db.JobOrders
            .AsNoTracking()
            .Include(j => j.BillingClient)
            .Include(j => j.LoadPort)
            .Include(j => j.DischargePort)
            .Where(j => j.CreatedOn >= from && j.CreatedOn <= to)
            .ToListAsync();

        await Task.WhenAll(awbsTask, exportsTask, jobsTask);

        // ── Map each source row to a canonical ActivityItem ──────────────────
        var awbItems = awbsTask.Result.Select(MapAwb).ToList();
        var expItems = exportsTask.Result.Select(MapExport).ToList();
        var allJobs  = jobsTask.Result.Select(j => (Item: MapJobOrder(j), Job: j)).ToList();

        // Split JobOrder rows by quadrant
        List<ActivityItem> JobsFor(JobShipmentMode m, JobShipmentType t) =>
            allJobs.Where(p => p.Job.ShipmentMode == m && p.Job.ShipmentType == t)
                   .Select(p => p.Item).ToList();

        var quadrants = new List<QuadrantReport>
        {
            BuildQuadrant(ReportMode.Air, ReportDirection.Import,
                awbItems.Concat(JobsFor(JobShipmentMode.Air, JobShipmentType.Import)).ToList()),
            BuildQuadrant(ReportMode.Air, ReportDirection.Export,
                JobsFor(JobShipmentMode.Air, JobShipmentType.Export)),
            BuildQuadrant(ReportMode.Sea, ReportDirection.Import,
                JobsFor(JobShipmentMode.Sea, JobShipmentType.Import)),
            BuildQuadrant(ReportMode.Sea, ReportDirection.Export,
                expItems.Concat(JobsFor(JobShipmentMode.Sea, JobShipmentType.Export)).ToList()),
        };

        return new ActivityReport
        {
            Period    = new ReportPeriod(label, from, to),
            Quadrants = quadrants,
        };
    }

    private static QuadrantReport BuildQuadrant(ReportMode mode, ReportDirection dir, List<ActivityItem> items)
    {
        return new QuadrantReport
        {
            Mode       = mode,
            Direction  = dir,
            Received   = items.Count(i => i.Stage == ReportStage.Received),
            InTransit  = items.Count(i => i.Stage == ReportStage.InTransit),
            AtPort     = items.Count(i => i.Stage == ReportStage.AtPort),
            Cleared    = items.Count(i => i.Stage == ReportStage.Cleared),
            Completed  = items.Count(i => i.Stage == ReportStage.Completed),
            Items      = items.OrderByDescending(i => i.ReceivedAt).ToList(),
        };
    }

    // ── Per-source mappers ───────────────────────────────────────────────────

    private static ActivityItem MapAwb(AwbShipment a) => new()
    {
        Source        = "AWB",
        Reference     = string.IsNullOrEmpty(a.HawbNo) ? $"AWB-{a.Id}" : a.HawbNo,
        ClientName    = a.ShipperName.Length > 0 ? a.ShipperName : a.ConsigneeName,
        Stage         = MapAwbStage(a.Status),
        CurrentStatus = a.Status.ToString(),
        Location      = !string.IsNullOrEmpty(a.DropLocation) ? a.DropLocation
                        : !string.IsNullOrEmpty(a.PickupLocation) ? a.PickupLocation
                        : a.DestinationStation,
        VesselName    = null,   // AWB workflow is air; no vessel
        VoyageNumber  = null,
        ReceivedAt    = a.ReceivedAt,
        LastEventAt   = a.InvoiceSentAt ?? a.CustomsDocsReceivedAt
                        ?? a.DeliveredAtPortAt ?? a.VehicleDetailsAt
                        ?? a.TransporterAssignedAt,
    };

    private static ActivityItem MapExport(ExportJob e) => new()
    {
        Source        = "Export",
        Reference     = string.IsNullOrEmpty(e.JobReference) ? $"EXP-{e.Id}" : e.JobReference,
        ClientName    = e.CustomerName,
        Stage         = MapExportStage(e.Status),
        CurrentStatus = e.Status.ToString(),
        Location      = e.ContainerNumber ?? null,  // shows container once stuffed
        VesselName    = e.VesselName,
        VoyageNumber  = e.VoyageNumber,
        ReceivedAt    = e.ReceivedAt,
        LastEventAt   = e.TerminalGateInAt ?? e.PortClearedAt
                        ?? e.CargoCollectedAt ?? e.VehicleAssignedAt
                        ?? e.PickupInitiatedAt,
    };

    private static ActivityItem MapJobOrder(JobOrder j) => new()
    {
        Source        = "JobOrder",
        Reference     = string.IsNullOrEmpty(j.JobOrderNo) ? $"JOB-{j.Id}" : j.JobOrderNo,
        ClientName    = j.BillingClient?.CompanyName ?? "—",
        Stage         = MapJobOrderStage(j.Status),
        CurrentStatus = j.Status.ToString(),
        Location      = (j.LoadPort != null || j.DischargePort != null)
                        ? $"{j.LoadPort?.PortCode ?? "?"} → {j.DischargePort?.PortCode ?? "?"}"
                        : null,
        VesselName    = null,   // JobOrder model doesn't carry vessel yet
        VoyageNumber  = null,
        ReceivedAt    = j.CreatedOn,
        LastEventAt   = j.ClosedOn ?? j.ApprovedOn ?? j.VerifiedOn ?? j.SubmittedOn,
    };

    // ── Status → canonical stage maps ────────────────────────────────────────

    private static ReportStage MapAwbStage(AwbStatus s) => s switch
    {
        AwbStatus.Received                                              => ReportStage.Received,
        AwbStatus.TransporterAssigned or AwbStatus.VehicleAssigned
            or AwbStatus.InTransit                                      => ReportStage.InTransit,
        AwbStatus.DeliveredAtPort or AwbStatus.CustomsPending           => ReportStage.AtPort,
        AwbStatus.CustomsCleared                                        => ReportStage.Cleared,
        AwbStatus.InvoiceSent or AwbStatus.Completed                    => ReportStage.Completed,
        _                                                               => ReportStage.Received,
    };

    private static ReportStage MapExportStage(ExportJobStatus s) => s switch
    {
        ExportJobStatus.Received                                        => ReportStage.Received,
        ExportJobStatus.PickupInitiated or ExportJobStatus.TransporterBooked
            or ExportJobStatus.VehicleAssigned
            or ExportJobStatus.CargoCollected                           => ReportStage.InTransit,
        ExportJobStatus.ChecklistPending or ExportJobStatus.ChecklistConfirmed
            or ExportJobStatus.IcegateSubmitted or ExportJobStatus.HandedToOps => ReportStage.AtPort,
        ExportJobStatus.PortCleared or ExportJobStatus.ExportReady
            or ExportJobStatus.BookingReceived or ExportJobStatus.SurveyInProgress
            or ExportJobStatus.LoadingAuthorized                        => ReportStage.Cleared,
        ExportJobStatus.CargoLoaded or ExportJobStatus.SiDoReceived
            or ExportJobStatus.InTransitToIctt
            or ExportJobStatus.TerminalGateIn                           => ReportStage.Completed,
        _                                                               => ReportStage.Received,
    };

    private static ReportStage MapJobOrderStage(JobOrderStatus s) => s switch
    {
        JobOrderStatus.Draft or JobOrderStatus.Submitted                => ReportStage.Received,
        JobOrderStatus.Verified                                         => ReportStage.InTransit,
        JobOrderStatus.Approved                                         => ReportStage.Cleared,
        JobOrderStatus.Closed                                           => ReportStage.Completed,
        JobOrderStatus.Rejected or JobOrderStatus.Reopened              => ReportStage.Received,
        _                                                               => ReportStage.Received,
    };
}
