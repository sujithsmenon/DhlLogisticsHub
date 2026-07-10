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

    public Task ValidateAsync(IWorkflowContext ctx)
    {
        var bill = B(ctx);
        if (ctx.Operation == WorkflowOperationType.Delete)
        {
            if (bill.Status != BillStatus.Draft)
                ctx.Abort("Only Draft bills can be deleted.");
            return Task.CompletedTask;
        }
        if (bill.BillingClientId == 0) ctx.Abort("Billing Client is required.");
        if (bill.Charges.Count == 0)   ctx.Abort("At least one charge is required.");
        return Task.CompletedTask;
    }

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
