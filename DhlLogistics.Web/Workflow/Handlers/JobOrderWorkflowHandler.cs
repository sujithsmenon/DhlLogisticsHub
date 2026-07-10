namespace DhlLogistics.Web.Workflow.Handlers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Workflow adapter for Clearance / Forwarding / Import job orders (all one <see cref="JobOrder"/>
/// entity — mode is <see cref="JobMode"/>, import/export is <see cref="JobShipmentType"/>). Holds the
/// domain logic that used to live inline in <see cref="JobOrderService"/>: numbering, persistence,
/// billing sync and timeline. Billing is delegated to the shared <see cref="BillService"/> so there
/// is one copy of that logic.
/// </summary>
public sealed class JobOrderWorkflowHandler : IWorkflowHandler
{
    private readonly AppDbContext _db;
    private readonly BillService  _bills;
    private readonly AccountingService _accounting;
    private readonly ILogger<JobOrderWorkflowHandler> _log;

    public JobOrderWorkflowHandler(AppDbContext db, BillService bills, AccountingService accounting,
                                   ILogger<JobOrderWorkflowHandler> log)
    {
        _db    = db;
        _bills = bills;
        _accounting = accounting;
        _log   = log;
    }

    /// <summary>
    /// Logs CurrencyId / CurrencyCode / JobOrderNo immediately before SaveChanges and validates the
    /// optional Currency foreign key. A non-null CurrencyId that is not present in the Currencies lookup
    /// would raise <c>FK_JobOrders_Currencies_CurrencyId</c> on insert; catching it here surfaces the exact
    /// offending value in the log with a clear message instead of an opaque Postgres 23503. We do NOT
    /// mutate the value (no silent null-coercion) or disable the FK — the fix is a correct/complete
    /// Currencies lookup + correct dropdown binding (Value = Currency.Id).
    /// </summary>
    private async Task LogAndCheckCurrencyAsync(JobOrder job)
    {
        string code;
        if (job.CurrencyId is null)
        {
            code = "(none)";
        }
        else
        {
            code = await _db.Currencies.Where(c => c.Id == job.CurrencyId)
                                       .Select(c => c.CurrencyCode).FirstOrDefaultAsync() ?? "(MISSING)";
            if (code == "(MISSING)")
                _log.LogError("JobOrder {JobNo}: CurrencyId={CurrencyId} does not exist in Currencies — " +
                              "this would violate FK_JobOrders_Currencies_CurrencyId. Check the currency " +
                              "dropdown binding (Value must be Currency.Id) and that the Currencies lookup is seeded.",
                              job.JobOrderNo, job.CurrencyId);
        }
        _log.LogInformation("JobOrder pre-save: JobOrderNo={JobNo} CurrencyId={CurrencyId} CurrencyCode={Code}",
                            string.IsNullOrEmpty(job.JobOrderNo) ? "(pending)" : job.JobOrderNo, job.CurrencyId, code);
    }

    public string Module     => "Job Order";
    public string EntityType => "JobOrder";

    private static JobOrder Job(IWorkflowContext ctx) => (JobOrder)ctx.Entity;

    public async Task ValidateAsync(IWorkflowContext ctx)
    {
        var job = Job(ctx);

        if (ctx.Operation == WorkflowOperationType.Delete)
        {
            // Deletable until it has been verified (no bills exist before Approval, so this is safe).
            if (job.Status != JobOrderStatus.Draft && job.Status != JobOrderStatus.Submitted)
                ctx.Abort("Only unverified jobs can be deleted; use Reject or Close otherwise.");

            // A job auto-creates a Draft bill, so a Draft bill alone can't block deletion — it cascades.
            // Bills that have progressed past Draft block deletion unless cascading is explicitly enabled.
            if (!ctx.CascadeDelete &&
                await _db.Bills.AnyAsync(b => b.JobOrderId == job.Id && b.Status != BillStatus.Draft))
                ctx.Abort("This job has bills in progress and cannot be deleted. Cancel the bills first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(job.CustomerInvoiceNumber))
            ctx.Abort("Customer Invoice No is required.");
        if (job.BillingClientId == 0) ctx.Abort("Billing Client is required.");
        if (job.ShipperId == 0)       ctx.Abort("Shipper is required.");
        if (job.ConsigneeId == 0)     ctx.Abort("Consignee is required.");
    }

    public async Task GenerateNumberAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation != WorkflowOperationType.Create) return;

        var job = Job(ctx);
        job.FinYear    = JobOrderService.ComputeFinYear(job.JobOrderDate);
        job.JobOrderNo = await NextJobOrderNoAsync(job.Mode, job.FinYear);
        // A newly created job is immediately "awaiting verification" so it enters the Verification queue
        // (which lists Submitted jobs) — the intended Create → Verify → Approve flow has no separate manual
        // Submit step. (SubmitAsync remains for re-submitting a Rejected job.)
        job.Status      = JobOrderStatus.Submitted;
        job.CreatedBy   = ctx.User;
        job.CreatedOn   = DateTime.UtcNow;
        job.SubmittedBy = ctx.User;
        job.SubmittedOn = DateTime.UtcNow;
    }

    public async Task PersistAsync(IWorkflowContext ctx)
    {
        var job = Job(ctx);
        switch (ctx.Operation)
        {
            case WorkflowOperationType.Create:
                int order = 1;
                foreach (var o in job.Operations) if (o.DisplayOrder == 0) o.DisplayOrder = order++;
                int co = 1;
                foreach (var c in job.Charges) if (c.DisplayOrder == 0) c.DisplayOrder = co++;
                JobOrderService.RecalcTotals(job);               // charge lines + rolled-up totals
                await LogAndCheckCurrencyAsync(job);             // pre-save: log + verify Currency FK
                _db.Add(job);                                    // operations + charges added with the graph
                await _db.SaveChangesAsync();
                break;

            case WorkflowOperationType.Update:
                job.ModifiedBy = ctx.User;
                job.ModifiedOn = DateTime.UtcNow;
                _db.Entry(job).State = EntityState.Modified;

                // MERGE operations (update existing by Id / add new / remove dropped). We do NOT use the
                // delete+re-add strategy here (unlike bill charges) because the accounting engine keys the
                // vendor-expense voucher on "{JobNo}#OP{opId}" — churning Ids would make every edit re-post a
                // duplicate expense/payable. Keeping Ids stable means an unrelated edit re-posts nothing,
                // while a cost entered later (previously skipped) still posts exactly once.
                var existingOps = await _db.JobOrderOperations.Where(o => o.JobOrderId == job.Id).ToListAsync();
                foreach (var ex in existingOps)
                    if (job.Operations.All(o => o.Id != ex.Id))
                        _db.JobOrderOperations.Remove(ex);
                int oo = 1;
                foreach (var o in job.Operations)
                {
                    o.JobOrderId = job.Id;
                    if (o.DisplayOrder == 0) o.DisplayOrder = oo;
                    oo++;
                    var match = o.Id != 0 ? existingOps.FirstOrDefault(e => e.Id == o.Id) : null;
                    if (match is null) { o.Id = 0; _db.JobOrderOperations.Add(o); }
                    else _db.Entry(match).CurrentValues.SetValues(o);
                }

                // REPLACE sale charges (delete existing + re-add). Unlike operations, charges carry no
                // accounting key, so churning Ids is safe and keeps the mapping to bill charges simple.
                var existingCharges = await _db.JobCharges.Where(c => c.JobOrderId == job.Id).ToListAsync();
                _db.JobCharges.RemoveRange(existingCharges);
                int uc = 1;
                foreach (var c in job.Charges)
                {
                    c.Id = 0;
                    c.JobOrderId = job.Id;
                    if (c.DisplayOrder == 0) c.DisplayOrder = uc;
                    uc++;
                    _db.JobCharges.Add(c);
                }
                JobOrderService.RecalcTotals(job);   // refresh rolled-up SubTotal / GstTotal / TotalAmount
                await LogAndCheckCurrencyAsync(job); // pre-save: log + verify Currency FK
                await _db.SaveChangesAsync();
                break;

            case WorkflowOperationType.Delete:
                await _bills.RemoveDraftBillsForJobAsync(job.Id);   // cascade untouched Draft bills
                _db.Remove(job);
                await _db.SaveChangesAsync();
                break;
        }
    }

    public async Task GenerateBillingAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation == WorkflowOperationType.Delete) return;   // handled in PersistAsync

        var job = Job(ctx);
        // Bills exist ONLY once a job is Approved (workflow rule #3). A Draft/Submitted/Verified job
        // raises no bill. Editing an ALREADY-Approved job keeps its bills in sync (rule #4) and posts any
        // vendor costs entered after approval (rule #6). All of this runs inside the orchestrator's
        // transaction, so job + bills + vouchers commit or roll back together (rule #11).
        if (job.Status != JobOrderStatus.Approved) return;

        await _bills.UpsertForJobAsync(job);
        await _accounting.PostJobExpensesAsync(job, ctx.User);
    }

    public async Task WriteTimelineAsync(IWorkflowContext ctx)
    {
        if (ctx.Operation == WorkflowOperationType.Delete) return;   // job row (and its events) gone

        var job = Job(ctx);
        var type = ctx.Operation == WorkflowOperationType.Create
            ? JobOrderEventType.Created
            : (job.Status == JobOrderStatus.Approved ? JobOrderEventType.PostVerifyEdited : JobOrderEventType.Updated);

        _db.Add(new JobOrderEvent
        {
            JobOrderId = job.Id,
            EventType  = type,
            Notes      = ctx.Operation == WorkflowOperationType.Create ? "Job created; submitted for verification." : null,
            Actor      = ctx.User,
            At         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public WorkflowDescriptor Describe(IWorkflowContext ctx)
    {
        var job     = Job(ctx);
        var verb    = ctx.Operation.ToString().ToLowerInvariant() + "d";   // created / updated / deleted
        var summary = $"{Module} {job.JobOrderNo} ({job.Mode}) {verb}.";
        return new WorkflowDescriptor(job.Id, job.JobOrderNo, summary);
    }

    // Single source of the job-order numbering (moved out of JobOrderService).
    private async Task<string> NextJobOrderNoAsync(JobMode mode, int finYear)
    {
        var prefix    = mode == JobMode.Clearance ? "CLR" : "FWD";
        var fyDisplay = $"{(finYear % 100):D2}-{((finYear + 1) % 100):D2}";

        var lastNo = await _db.Set<JobOrder>()
            .Where(j => j.Mode == mode && j.FinYear == finYear)
            .OrderByDescending(j => j.Id)
            .Select(j => j.JobOrderNo)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo))
        {
            var tail = lastNo.Split('/').Last();
            if (int.TryParse(tail, out var n)) seq = n + 1;
        }
        return $"{prefix}/{fyDisplay}/{seq:D4}";
    }
}
