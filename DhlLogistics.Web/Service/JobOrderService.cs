namespace DhlLogistics.Web.Service;

using System.Security.Claims;
using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

public class JobOrderService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authProvider;

    public JobOrderService(AppDbContext db, AuthenticationStateProvider authProvider)
    {
        _db = db;
        _authProvider = authProvider;
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

    public Task<List<JobOrder>> GetByStatusAsync(params JobOrderStatus[] statuses) =>
        WithRefs().Where(j => statuses.Contains(j.Status)).OrderByDescending(j => j.Id).ToListAsync();

    public Task<JobOrder?> GetByIdAsync(long id) =>
        WithRefs().Include(j => j.Events).FirstOrDefaultAsync(j => j.Id == id);

    // ── Numbering ────────────────────────────────────────────────────────────

    public static int ComputeFinYear(DateTime d) => d.Month >= 4 ? d.Year : d.Year - 1;

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

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task<JobOrder> CreateAsync(JobOrder job)
    {
        var user = await CurrentUserAsync();
        job.FinYear    = ComputeFinYear(job.JobOrderDate);
        job.JobOrderNo = await NextJobOrderNoAsync(job.Mode, job.FinYear);
        job.Status     = JobOrderStatus.Draft;
        job.CreatedBy  = user;
        job.CreatedOn  = DateTime.UtcNow;
        _db.Add(job);
        await _db.SaveChangesAsync();

        await LogEventAsync(job.Id, JobOrderEventType.Created, $"Job created as Draft.", user);
        return job;
    }

    public async Task UpdateAsync(JobOrder job)
    {
        var user = await CurrentUserAsync();
        job.ModifiedBy = user;
        job.ModifiedOn = DateTime.UtcNow;
        _db.Entry(job).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        await LogEventAsync(job.Id,
            job.Status == JobOrderStatus.Approved ? JobOrderEventType.PostVerifyEdited : JobOrderEventType.Updated,
            null, user);
    }

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
    }

    public async Task ApproveAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var j = await _db.Set<JobOrder>().FindAsync(id) ?? throw new KeyNotFoundException();
        if (j.Status != JobOrderStatus.Verified)
            throw new InvalidOperationException($"Only Verified jobs can be approved (current: {j.Status}).");

        j.Status     = JobOrderStatus.Approved;
        j.ApprovedBy = user;
        j.ApprovedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, JobOrderEventType.Approved, note, user);
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
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var j = await _db.Set<JobOrder>().FindAsync(id);
        if (j is null) return false;
        if (j.Status != JobOrderStatus.Draft)
            throw new InvalidOperationException("Only Draft jobs can be deleted; use Reject or Close otherwise.");

        _db.Remove(j);
        await _db.SaveChangesAsync();
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
