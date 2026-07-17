namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Maps any shipment/job type (AWB shipment, Export job, Clearance/Forwarding JobOrder) into a
/// <b>prepared</b> Transportation <see cref="Bill"/> — a Draft in memory only. Persistence, numbering,
/// verification, approval and accounting all reuse the existing Bill workflow (<see cref="BillService"/>,
/// <see cref="Workflow.Handlers.BillWorkflowHandler"/>, <see cref="AccountingService"/>): this service adds
/// no billing logic, it only fills the bill's header + transport snapshot from source data. That keeps a
/// single billing/approval/accounts pipeline while letting every shipment type raise a Transportation bill
/// without a separate module or duplicated code.
/// </summary>
public class TransportationBillService
{
    private readonly AppDbContext _db;

    public TransportationBillService(AppDbContext db) => _db = db;

    /// <summary>Business rule: a Transportation bill raised from a Clearance/Forwarding job must inherit the
    /// job's Transportation charges — so when the job has none, the bill cannot be created at all. Surfaced
    /// as-is by every launch point (job lists, BillPopup, auto-billing, workflow validation).</summary>
    public const string NoTransportChargesMessage =
        "No transportation-related charges found. Please add Transportation charges in the Job before raising the Transportation Bill.";

    // ── Job → Transportation-bill charge inheritance ──────────────────────────
    // Rule: ONLY ChargeCategory.Transport lines flow onto a job-raised TB bill. Clearance/Customs,
    // Documentation, CHA, Warehouse etc. belong to the job's primary CB/FB bill and are never copied.

    /// <summary>The job's billable Transportation charge lines (Cancelled-operation lines excluded, same rule
    /// as the primary-bill inheritance in <see cref="BillService.UpsertForJobAsync"/>) plus the operation-name
    /// snapshot used to label the lines on the bill. Static so <see cref="BillService"/> and the workflow
    /// handler share this one copy of the filter.</summary>
    public static async Task<(List<JobCharge> Charges, Dictionary<long, string> OpNames)> LoadTransportChargesAsync(
        AppDbContext db, long jobOrderId)
    {
        var charges = await db.JobCharges
            .Where(c => c.JobOrderId == jobOrderId && c.Category == ChargeCategory.Transport)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .ToListAsync();

        var ops = await db.JobOperations
            .Where(o => o.JobOrderId == jobOrderId)
            .Select(o => new { o.Id, o.OperationType, o.Status })
            .ToListAsync();
        var cancelled = ops.Where(o => o.Status == JobOperationStatus.Cancelled).Select(o => o.Id).ToHashSet();

        charges.RemoveAll(c => c.JobOperationId.HasValue && cancelled.Contains(c.JobOperationId.Value));
        return (charges, ops.ToDictionary(o => o.Id, o => o.OperationType));
    }

    /// <summary>True when the job has at least one billable Transportation charge — the launch-point guard
    /// the job lists check before even opening the New-Transportation-Bill popup.</summary>
    public async Task<bool> HasTransportChargesAsync(long jobOrderId) =>
        (await LoadTransportChargesAsync(_db, jobOrderId)).Charges.Count > 0;

    /// <summary>
    /// Idempotent job→bill charge sync, keyed on <see cref="BillCharge.SourceJobChargeId"/>: matched lines are
    /// updated IN PLACE (never delete-then-reinsert — the scoped DbContext may already track them), missing
    /// lines are inserted, and job-sourced lines whose JobCharge was deleted (or re-categorised away from
    /// Transport) are removed. Manually keyed lines (SourceJobChargeId == null) are never touched. Safe to run
    /// any number of times — repeated refreshes can never duplicate a row. Amount/GST/Net are left to
    /// <see cref="BillService.RecalcTotals"/>, which every caller runs right after.
    /// </summary>
    public static void ApplyTransportCharges(Bill bill, List<JobCharge> src, Dictionary<long, string> opNames)
    {
        var srcIds = src.Select(c => c.Id).ToHashSet();
        foreach (var stale in bill.Charges.Where(c => c.SourceJobChargeId is { } sid && !srcIds.Contains(sid)).ToList())
            bill.Charges.Remove(stale);

        var bydSource = bill.Charges
            .Where(c => c.SourceJobChargeId.HasValue)
            .ToDictionary(c => c.SourceJobChargeId!.Value);

        foreach (var jc in src)
        {
            if (!bydSource.TryGetValue(jc.Id, out var row))
            {
                row = new BillCharge { SourceJobChargeId = jc.Id };
                bill.Charges.Add(row);
            }
            row.JobOperationId = jc.JobOperationId;
            row.OperationName  = jc.JobOperationId.HasValue && opNames.TryGetValue(jc.JobOperationId.Value, out var opName)
                                     ? opName : null;
            row.Category     = jc.Category;
            row.ChargeCodeId = jc.ChargeCodeId;
            row.SacId        = jc.SacId;
            row.Description  = jc.Description;
            row.Quantity     = jc.Quantity;
            row.Rate         = jc.Rate;
            row.GstRate      = jc.GstRate;
            row.DisplayOrder = jc.DisplayOrder;
        }
    }

    /// <summary>
    /// Seeds a job-raised Transportation bill (new, unsaved) with the job's Transportation charges.
    /// Returns false — and leaves the bill chargeless — when the job has none, so the caller can refuse
    /// to create the bill and show <see cref="NoTransportChargesMessage"/>.
    /// </summary>
    public async Task<bool> PopulateJobChargesAsync(Bill bill)
    {
        if (bill.JobOrderId is not { } jobId) return false;
        var (src, opNames) = await LoadTransportChargesAsync(_db, jobId);
        if (src.Count == 0) return false;
        ApplyTransportCharges(bill, src, opNames);
        BillService.RecalcTotals(bill);
        return true;
    }

    /// <summary>
    /// Draft-only synchronization on open/refresh: reloads a job-raised Transportation bill's inherited
    /// charges from its JobOrder and persists any difference immediately (so a Submit right after opening
    /// uses the synced totals). No-ops for anything else — once a bill is Submitted / Verified / Approved
    /// its charges are immutable and never re-synchronized, and approved bills are untouched by design.
    /// The bill must be tracked with its Charges loaded (as <see cref="BillService.GetByIdAsync"/> returns).
    /// </summary>
    public async Task SyncDraftBillFromJobAsync(Bill bill)
    {
        if (bill.Id == 0 || bill.Status != BillStatus.Draft || bill.Mode != BillMode.Transportation) return;
        if (bill.JobOrderId is not { } jobId) return;

        var (src, opNames) = await LoadTransportChargesAsync(_db, jobId);
        ApplyTransportCharges(bill, src, opNames);
        BillService.RecalcTotals(bill);

        if (!_db.ChangeTracker.HasChanges()) return;   // nothing drifted — no write, no event
        _db.BillEvents.Add(new BillEvent
        {
            BillId    = bill.Id,
            EventType = BillEventType.Updated,
            Notes     = $"Transportation charges re-synced from job (now {src.Count} inherited line(s), total {bill.TotalAmount:N2}).",
            Actor     = "auto-sync",
            At        = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Active transporters for the bill's transporter picker (reused from the AWB/Export masters).</summary>
    public Task<List<Transporter>> GetTransportersAsync() =>
        _db.Transporters.Where(t => t.IsActive).OrderBy(t => t.CompanyName).ToListAsync();

    // ── Source → prepared Transportation Bill ─────────────────────────────────

    /// <summary>Prepare a Draft Transportation bill from an AWB shipment. Billing client / currency are left
    /// for the user to pick (AWB stores parties as free text, not client FKs); everything else is snapshotted.</summary>
    public async Task<Bill> PrepareFromAwbAsync(long awbId)
    {
        var a = await _db.AwbShipments
            .Include(x => x.Transporter)
            .FirstOrDefaultAsync(x => x.Id == (int)awbId)
            ?? throw new InvalidOperationException("AWB shipment not found.");

        var bill = NewTransportBill();
        bill.SourceType       = BillSourceType.AwbShipment;
        bill.SourceId         = a.Id;
        bill.SourceReference  = a.HawbNo;
        bill.ShipmentTypeName = "AWB Shipment (Air)";
        bill.AwbOrBlNumber    = a.HawbNo;
        bill.Reference        = a.ConsigneeName;              // visible until a billing client is chosen
        bill.Origin           = a.OriginStation;
        bill.Destination      = a.DestinationStation;
        bill.PickupLocation   = a.PickupLocation;
        bill.DeliveryLocation = a.DropLocation;
        bill.VehicleNumber    = a.VehicleNumber;
        bill.DriverName       = a.DriverName;
        bill.TransporterId    = a.TransporterId;
        bill.CommodityName    = a.GoodsDescription;
        bill.Quantity         = a.Pieces;
        bill.WeightKg         = (decimal)a.GrossWeightKg;
        bill.VolumeCbm        = (decimal)a.VolumeCbm;
        // The CUSTOMER's reference (Billing Group key) — NOT a.InvoiceNumber, which is the Stage-5 invoice we
        // raise TO DHL. Seeding the group key from that would file the bill under our own invoice number.
        bill.CustomerInvoiceNumber = a.CustomerInvoiceNumber;
        bill.Remarks          = a.HandlingInfo;
        return bill;
    }

    /// <summary>Prepare a Draft Transportation bill from an Export job.</summary>
    public async Task<Bill> PrepareFromExportAsync(long exportId)
    {
        var e = await _db.ExportJobs
            .Include(x => x.Transporter)
            .FirstOrDefaultAsync(x => x.Id == (int)exportId)
            ?? throw new InvalidOperationException("Sea shipment not found.");

        var bill = NewTransportBill();
        bill.SourceType       = BillSourceType.ExportJob;
        bill.SourceId         = e.Id;
        bill.SourceReference  = e.JobReference;
        bill.ShipmentTypeName = "Sea Shipment";   // display label; bills issued before the rename keep "Export Job"
        bill.AwbOrBlNumber    = e.ShippingBillNumber;
        bill.ContainerNumber  = e.ContainerNumber;
        bill.Reference        = e.CustomerName;
        bill.Destination      = e.VesselName;                 // export routing: vessel/voyage as destination
        bill.VehicleNumber    = e.VehicleNumber;
        bill.DriverName       = e.DriverName;
        bill.TransporterId    = e.TransporterId;
        bill.CommodityName    = e.CargoDescription;
        bill.Quantity         = e.Pieces;
        bill.WeightKg         = (decimal)e.GrossWeightKg;
        bill.CustomerInvoiceNumber = e.CustomerInvoiceNumber;   // Billing Group key
        bill.Remarks          = e.Notes;
        return bill;
    }

    /// <summary>Base Draft Transportation bill with a single seeded Freight charge line, so the bill satisfies
    /// the workflow's "at least one charge" rule and the user only enters the amount before submitting.</summary>
    private static Bill NewTransportBill()
    {
        var bill = new Bill
        {
            Mode         = BillMode.Transportation,
            BillDate     = DateTime.UtcNow.Date,
            ExchangeRate = 1m,
        };
        bill.Charges.Add(new BillCharge
        {
            Category     = ChargeCategory.Transport,
            Description  = "Freight Charges",
            Quantity     = 1m,
            Rate         = 0m,
            GstRate      = 0m,
            DisplayOrder = 1,
        });
        return bill;
    }
}
