namespace DhlLogistics.Web.Workflow.Handlers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Service;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Workflow adapter for manually-raised bills — Transportation (TB), plus Clearance (CB) and
/// Forwarding (FB) bills created from the bill list pages. Numbering, totals and charge replacement
/// reuse the static helpers on <see cref="BillService"/> so there is one copy of that logic. (The
/// job-driven auto-billing path stays in <see cref="BillService.UpsertForJobAsync"/> and is NOT
/// orchestrated, to avoid nesting inside the JobOrder workflow transaction.)
/// </summary>
public sealed class BillWorkflowHandler : IWorkflowHandler
{
    private readonly AppDbContext _db;
    public BillWorkflowHandler(AppDbContext db) => _db = db;

    public string Module     => "Billing";
    public string EntityType => "Bill";

    private static Bill B(IWorkflowContext ctx) => (Bill)ctx.Entity;

    public async Task ValidateAsync(IWorkflowContext ctx)
    {
        var bill = B(ctx);
        if (ctx.Operation == WorkflowOperationType.Delete)
        {
            if (bill.Status != BillStatus.Draft)
                ctx.Abort("Only Draft bills can be deleted.");
            return;
        }
        if (bill.BillingClientId == 0) ctx.Abort("Billing Client is required.");
        if (bill.Charges.Count == 0)   ctx.Abort("At least one charge is required.");

        // A Transportation bill raised from a Clearance/Forwarding job inherits the job's Transport-category
        // charges — so it cannot come into existence while the job has none. Enforced here (the choke point
        // every manual create passes through); the job-list launchers and BillPopup pre-check only for UX.
        // Standalone TB bills and AWB/Export-sourced ones have no JobOrder and are unaffected.
        if (ctx.Operation == WorkflowOperationType.Create
            && bill.Mode == BillMode.Transportation
            && bill.JobOrderId is { } jobId2)
        {
            var (src, _) = await TransportationBillService.LoadTransportChargesAsync(_db, jobId2);
            if (src.Count == 0) ctx.Abort(TransportationBillService.NoTransportChargesMessage);
        }

        await InheritCustomerInvoiceNumberAsync(bill);
    }

    /// <summary>
    /// The Billing Group invariant — the SINGLE authoritative rule, enforced at the one choke point every
    /// bill write passes through (Create AND Update, from every page: bill lists, the Job / AWB / Export
    /// "raise bill" launchers, and any creation path added in future).
    ///
    /// <para>A bill raised from ANY of the four operational modules — Clearance Job, Forwarding Job, Export
    /// Job, AWB Shipment — carries that source's CustomerInvoiceNumber, and the SOURCE is authoritative: the
    /// value is re-read from it on every save, so the bill cannot drift out of its group. The individual
    /// Prepare* mappings still populate the field for the UI, but they are a convenience, not the guarantee;
    /// this method is the guarantee.</para>
    ///
    /// <para>Un-linking a bill from its source also CLEARS the reference — otherwise a bill that once pointed
    /// at job A would keep A's reference, stay in A's Billing Group, and be pulled into A's consolidated
    /// invoice. A genuinely standalone bill keeps whatever the user typed.</para>
    ///
    /// <para>Null stays null: a source with no CustomerInvoiceNumber (every legacy AWB / Export row) yields a
    /// bill with none, which forms no group and behaves exactly as it does today.</para>
    /// </summary>
    private async Task InheritCustomerInvoiceNumberAsync(Bill bill)
    {
        // A job-linked bill always wins on JobOrderId (Clearance / Forwarding, and job-raised Transportation).
        if (bill.JobOrderId is { } jobId)
        {
            var jobRef = await _db.JobOrders.AsNoTracking()
                .Where(j => j.Id == jobId)
                .Select(j => j.CustomerInvoiceNumber)
                .FirstOrDefaultAsync();
            bill.CustomerInvoiceNumber = Clean(jobRef);
            return;
        }

        // Otherwise fall back to the generic billing source (AWB shipment / Export job).
        switch (bill.SourceType)
        {
            case BillSourceType.AwbShipment when bill.SourceId is { } awbId:
            {
                // NOTE: AwbShipment.CustomerInvoiceNumber — deliberately NOT AwbShipment.InvoiceNumber, which
                // is the Stage-5 invoice raised TO DHL and is an unrelated document.
                var awbRef = await _db.AwbShipments.AsNoTracking()
                    .Where(a => a.Id == (int)awbId)
                    .Select(a => a.CustomerInvoiceNumber)
                    .FirstOrDefaultAsync();
                bill.CustomerInvoiceNumber = Clean(awbRef);
                return;
            }
            case BillSourceType.ExportJob when bill.SourceId is { } expId:
            {
                var expRef = await _db.ExportJobs.AsNoTracking()
                    .Where(e => e.Id == (int)expId)
                    .Select(e => e.CustomerInvoiceNumber)
                    .FirstOrDefaultAsync();
                bill.CustomerInvoiceNumber = Clean(expRef);
                return;
            }
        }

        // Truly standalone bill — nothing to inherit from. Leave the user-entered value (may be null) alone.
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task GenerateNumberAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation != WorkflowOperationType.Create) return;

        var bill = B(ctx);
        bill.FinYear   = BillService.ComputeFinYear(bill.BillDate);
        bill.BillNo    = await BillService.NextBillNoAsync(_db, bill.Mode, bill.FinYear);
        bill.Status    = BillStatus.Draft;
        bill.CreatedBy = ctx.User;
        bill.CreatedOn = DateTime.UtcNow;
    }

    public async Task PersistAsync(IWorkflowContext ctx)
    {
        var bill = B(ctx);
        switch (ctx.Operation)
        {
            case WorkflowOperationType.Create:
                BillService.RecalcTotals(bill);
                int order = 1;
                foreach (var c in bill.Charges) if (c.DisplayOrder == 0) c.DisplayOrder = order++;
                _db.Bills.Add(bill);
                await _db.SaveChangesAsync();
                break;

            case WorkflowOperationType.Update:
                bill.ModifiedBy = ctx.User;
                bill.ModifiedOn = DateTime.UtcNow;
                BillService.RecalcTotals(bill);
                _db.Entry(bill).State = EntityState.Modified;

                // Charge sync by primary key — update existing lines in place, insert only genuinely
                // new (Id == 0) lines, and delete only the lines the user removed. NEVER delete-then-
                // reinsert existing rows: the former code reset every line's Id to 0 and re-Added it,
                // but because Blazor Server's scoped DbContext was already tracking the charges loaded in
                // OnInitializedAsync, resetting the key repurposed the tracked (Deleted) entity into an
                // Insert — so the original rows were never deleted and brand-new rows landed alongside
                // them, duplicating every charge on each save (esp. bills auto-created from a job, which
                // already carry an Id + charges when opened).
                var incoming = bill.Charges.ToList();
                var keepIds  = incoming.Where(c => c.Id != 0).Select(c => c.Id).ToHashSet();

                // Ids currently persisted for this bill (scalar projection → no tracking, so we never
                // pull a conflicting second instance of an already-tracked charge).
                var dbIds = await _db.BillCharges
                    .Where(c => c.BillId == bill.Id)
                    .Select(c => c.Id)
                    .ToListAsync();
                foreach (var removedId in dbIds.Where(id => !keepIds.Contains(id)))
                {
                    var tracked = _db.ChangeTracker.Entries<BillCharge>()
                        .FirstOrDefault(e => e.Entity.Id == removedId);
                    if (tracked is not null) tracked.State = EntityState.Deleted;
                    else _db.BillCharges.Remove(new BillCharge { Id = removedId });
                }

                int o = 1;
                foreach (var c in incoming)
                {
                    c.BillId = bill.Id;
                    if (c.DisplayOrder == 0) c.DisplayOrder = o;
                    o++;
                    if (c.Id == 0)
                        _db.BillCharges.Add(c);                     // brand-new line
                    else
                        _db.Entry(c).State = EntityState.Modified;  // existing line — update in place
                }
                await _db.SaveChangesAsync();
                break;

            case WorkflowOperationType.Delete:
                _db.Bills.Remove(bill);                 // BillCharges / BillEvents cascade
                await _db.SaveChangesAsync();
                break;
        }
    }

    public Task GenerateBillingAsync(IWorkflowContext ctx) => Task.CompletedTask;   // a bill has no sub-bill

    public async Task WriteTimelineAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation == WorkflowOperationType.Delete) return;   // bill row (and its events) gone

        var bill = B(ctx);
        _db.BillEvents.Add(new BillEvent
        {
            BillId    = bill.Id,
            EventType = ctx.Operation == WorkflowOperationType.Create ? BillEventType.Created : BillEventType.Updated,
            Notes     = ctx.Operation == WorkflowOperationType.Create ? "Bill created as Draft." : null,
            Actor     = ctx.User,
            At        = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public WorkflowDescriptor Describe(IWorkflowContext ctx)
    {
        var bill = B(ctx);
        var verb = ctx.Operation.ToString().ToLowerInvariant() + "d";
        return new WorkflowDescriptor(bill.Id, bill.BillNo, $"{bill.Mode} Bill {bill.BillNo} {verb}.");
    }
}
