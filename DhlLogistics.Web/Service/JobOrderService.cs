namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Workflow;
using DhlLogistics.Web.Workflow.Handlers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Application service for job orders. Add/Edit/Delete are routed through the shared
/// <see cref="WorkflowOrchestrator"/> (numbering, persistence, auto-billing, timeline, activity,
/// audit, dashboard, notification) via <see cref="JobOrderWorkflowHandler"/>. Queries and the
/// status-lifecycle transitions stay here.
/// </summary>
public class JobOrderService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authProvider;
    private readonly DashboardState _dash;
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly JobOrderWorkflowHandler _handler;
    private readonly BillService _bills;
    private readonly AccountingService _accounting;
    private readonly WorkflowLogService _wflog;

    public JobOrderService(AppDbContext db, AuthenticationStateProvider authProvider,
                           DashboardState dash, WorkflowOrchestrator orchestrator,
                           JobOrderWorkflowHandler handler, BillService bills,
                           AccountingService accounting, WorkflowLogService wflog)
    {
        _db = db;
        _authProvider = authProvider;
        _dash = dash;
        _orchestrator = orchestrator;
        _handler = handler;
        _bills = bills;
        _accounting = accounting;
        _wflog = wflog;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    private IQueryable<JobOrder> WithRefs() => _db.Set<JobOrder>()
        .Include(j => j.Branch)
        .Include(j => j.BillingClient)
        .Include(j => j.Shipper)
        .Include(j => j.Consignee)
        .Include(j => j.LoadPort)
        .Include(j => j.DischargePort)
        .Include(j => j.Commodity)
        .Include(j => j.ContainerSize)
        .Include(j => j.SaleStaff)
        .Include(j => j.Currency);

    public Task<List<JobOrder>> GetByModeAsync(JobMode mode) =>
        WithRefs().Where(j => j.Mode == mode).OrderByDescending(j => j.Id).ToListAsync();

    /// <summary>All job orders across every mode (Clearance + Forwarding, Import/Export
    /// via <see cref="JobOrder.ShipmentType"/>) — feeds the master Operations → Jobs dashboard.</summary>
    public Task<List<JobOrder>> GetAllAsync() =>
        WithRefs().OrderByDescending(j => j.Id).ToListAsync();

    public Task<List<JobOrder>> GetByStatusAsync(params JobOrderStatus[] statuses) =>
        WithRefs().Where(j => statuses.Contains(j.Status)).OrderByDescending(j => j.Id).ToListAsync();

    public Task<JobOrder?> GetByIdAsync(long id) =>
        WithRefs()
            .Include(j => j.Events)
            .Include(j => j.Operations)
            .Include(j => j.Charges).ThenInclude(c => c.ChargeCode)
            .Include(j => j.Charges).ThenInclude(c => c.Sac)
            .FirstOrDefaultAsync(j => j.Id == id);

    // ── Numbering ────────────────────────────────────────────────────────────

    public static int ComputeFinYear(DateTime d) => d.Month >= 4 ? d.Year : d.Year - 1;

    // ── Sale-charge totals ─────────────────────────────────────────────────────
    /// <summary>Recomputes each charge line (Amount / GST / Net) and the job's rolled-up
    /// SubTotal / GstTotal / TotalAmount. Same rules as <see cref="BillService.RecalcTotals"/>
    /// (Discount lines subtract) so the job totals match the bill they generate.</summary>
    public static void RecalcTotals(JobOrder job)
    {
        decimal sub = 0, gst = 0, total = 0;
        foreach (var c in job.Charges)
        {
            var sign = c.Category == ChargeCategory.Discount ? -1m : 1m;
            c.Amount    = decimal.Round(c.Quantity * c.Rate, 2);
            c.GstAmount = decimal.Round(c.Amount * c.GstRate / 100m, 2);
            c.NetAmount = c.Amount + c.GstAmount;
            sub   += sign * c.Amount;
            gst   += sign * c.GstAmount;
            total += sign * c.NetAmount;
        }
        job.SubTotal    = sub;
        job.GstTotal    = gst;
        job.TotalAmount = total;
    }

    // ── Add / Edit / Delete — routed through the Workflow Engine ───────────────
    // All three run the shared pipeline (validate → tx → number → persist → billing →
    // timeline → activity → audit → commit → dashboard/search/notify). Domain specifics
    // (numbering, save, auto-billing, timeline) live in JobOrderWorkflowHandler.

    public async Task<JobOrder> CreateAsync(JobOrder job)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Create, await CurrentUserAsync(), job, _handler);
        await _orchestrator.RunAsync(ctx);
        return job;
    }

    public async Task UpdateAsync(JobOrder job)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Update, await CurrentUserAsync(), job, _handler);
        await _orchestrator.RunAsync(ctx);
    }

    // ── Status lifecycle ───────────────────────────────────────────────────────

    public async Task SubmitAsync(long id)
    {
        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Draft && j.Status != JobOrderStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit a job in '{j.Status}' status.");

        j.Status      = JobOrderStatus.Submitted;
        j.SubmittedBy = user;
        j.SubmittedOn = DateTime.UtcNow;
        j.RejectedBy  = null; j.RejectedOn = null; j.RejectionReason = null;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Submitted, "Submitted for verification.", user);
        await _wflog.LogTransitionAsync("Job Order", "JobOrder", id, j.JobOrderNo, WorkflowOperationType.Submit, user, $"Job {j.JobOrderNo} submitted for verification.");
        _dash.NotifyChanged();
    }

    public async Task VerifyAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted jobs can be verified (current: {j.Status}).");

        j.Status     = JobOrderStatus.Verified;
        j.VerifiedBy = user;
        j.VerifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Verified, note, user);
        await _wflog.LogTransitionAsync("Job Order", "JobOrder", id, j.JobOrderNo, WorkflowOperationType.Verify, user, $"Job {j.JobOrderNo} verified; awaiting approval.");
        _dash.NotifyChanged();
    }

    public async Task ApproveAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Verified)
            throw new InvalidOperationException($"Only Verified jobs can be approved (current: {j.Status}).");

        // Approval is the single trigger that brings a job into Billing + Accounts (rules #3, #11). The
        // status change, the auto-created bills (CB/FB, and TB if the job carries a transport leg) and the
        // vendor expense/payable postings all commit atomically or roll back together.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            j.Status     = JobOrderStatus.Approved;
            j.ApprovedBy = user;
            j.ApprovedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogEventAsync(id, JobOrderEventType.Approved, note, user);

            await _bills.UpsertForJobAsync(j, user);          // generate the linked bill(s) — Draft/Pending
            await _accounting.PostJobExpensesAsync(j, user);  // post any vendor costs already entered
            await _wflog.LogTransitionAsync("Job Order", "JobOrder", id, j.JobOrderNo, WorkflowOperationType.Approve, user, $"Job {j.JobOrderNo} approved; bill(s) generated.", notifyManagers: true);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        _dash.NotifyChanged();
    }

    public async Task RejectAsync(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Rejection reason is required.");

        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Submitted && j.Status != JobOrderStatus.Verified)
            throw new InvalidOperationException($"Cannot reject a job in '{j.Status}' status.");

        j.Status          = JobOrderStatus.Rejected;
        j.RejectedBy      = user;
        j.RejectedOn      = DateTime.UtcNow;
        j.RejectionReason = reason;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Rejected, reason, user);
        await _wflog.LogTransitionAsync("Job Order", "JobOrder", id, j.JobOrderNo, WorkflowOperationType.Reject, user, $"Job {j.JobOrderNo} rejected: {reason}", notifyManagers: true);
        _dash.NotifyChanged();
    }

    public async Task CloseAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Approved)
            throw new InvalidOperationException($"Only Approved jobs can be closed (current: {j.Status}).");

        j.Status   = JobOrderStatus.Closed;
        j.ClosedBy = user;
        j.ClosedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Closed, note, user);
        await _wflog.LogTransitionAsync("Job Order", "JobOrder", id, j.JobOrderNo, WorkflowOperationType.Close, user, $"Job {j.JobOrderNo} closed.");
        _dash.NotifyChanged();
    }

    public async Task ReopenAsync(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Reopen reason is required.");

        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Approved && j.Status != JobOrderStatus.Closed)
            throw new InvalidOperationException("Only Approved/Closed jobs can be reopened for post-verify modification.");

        j.Status = JobOrderStatus.Reopened;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Reopened, reason, user);
        _dash.NotifyChanged();
    }

    // Delete runs through the workflow too: the handler blocks deletion when non-Draft bills exist
    // and cascade-removes untouched Draft bills within the same transaction.
    public async Task<bool> DeleteAsync(long id, bool cascade = false)
    {
        var j = await _db.Set<JobOrder>().FindAsync(id);
        if (j is null) return false;

        var ctx = new WorkflowContext(WorkflowOperationType.Delete, await CurrentUserAsync(), j, _handler, cascade);
        await _orchestrator.RunAsync(ctx);
        return true;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private async Task LogEventAsync(long jobId, JobOrderEventType type, string? notes, string actor)
    {
        _db.Add(new JobOrderEvent
        {
            JobOrderId = jobId,
            EventType  = type,
            Notes      = notes,
            Actor      = actor,
            At         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
