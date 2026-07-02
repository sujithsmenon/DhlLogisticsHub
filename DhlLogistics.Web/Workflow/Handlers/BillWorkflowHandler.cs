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

                // Replace charges (delete + re-add) — same strategy as the former BillService.UpdateAsync.
                var existing = await _db.BillCharges.Where(c => c.BillId == bill.Id).ToListAsync();
                _db.BillCharges.RemoveRange(existing);
                int o = 1;
                foreach (var c in bill.Charges)
                {
                    c.Id = 0;
                    c.BillId = bill.Id;
                    if (c.DisplayOrder == 0) c.DisplayOrder = o;
                    o++;
                    _db.BillCharges.Add(c);
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
