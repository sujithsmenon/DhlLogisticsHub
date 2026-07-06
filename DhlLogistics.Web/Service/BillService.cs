namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Workflow;
using DhlLogistics.Web.Workflow.Handlers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>Per-job billing rollup for the master Jobs dashboard.</summary>
public record JobBillInfo(BillStatus Status, bool Issued);

public class BillService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authProvider;
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly BillWorkflowHandler _handler;
    private readonly AccountingService _accounting;
    private readonly DashboardState _dash;
    private readonly WorkflowLogService _wflog;

    public BillService(AppDbContext db, AuthenticationStateProvider authProvider,
                       WorkflowOrchestrator orchestrator, BillWorkflowHandler handler,
                       AccountingService accounting, DashboardState dash, WorkflowLogService wflog)
    {
        _db = db;
        _authProvider = authProvider;
        _orchestrator = orchestrator;
        _handler = handler;
        _accounting = accounting;
        _dash = dash;
        _wflog = wflog;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ──────────────────────────────────────────────────────────────
    private IQueryable<Bill> WithRefs() => _db.Bills
        .Include(b => b.Branch)
        .Include(b => b.BillingClient)
        .Include(b => b.Currency)
        .Include(b => b.JobOrder);

    public Task<List<Bill>> GetByModeAsync(BillMode mode) =>
        WithRefs().Where(b => b.Mode == mode).OrderByDescending(b => b.Id).ToListAsync();

    public Task<List<Bill>> GetByStatusAsync(params BillStatus[] statuses) =>
        WithRefs().Where(b => statuses.Contains(b.Status)).OrderByDescending(b => b.Id).ToListAsync();

    /// <summary>All bills raised against a given JobOrder (any mode / status).</summary>
    public Task<List<Bill>> GetByJobOrderAsync(long jobOrderId) =>
        WithRefs().Where(b => b.JobOrderId == jobOrderId).OrderByDescending(b => b.Id).ToListAsync();

    /// <summary>True when at least one bill is linked to the JobOrder — used to block job deletion.</summary>
    public Task<bool> AnyForJobAsync(long jobOrderId) =>
        _db.Bills.AnyAsync(b => b.JobOrderId == jobOrderId);

    /// <summary>Compact per-job billing summary — the most-advanced bill status and
    /// whether any linked bill has been issued as an invoice. One grouped query;
    /// used by the master Jobs dashboard to show Bill / Invoice status per row.</summary>
    public async Task<Dictionary<long, JobBillInfo>> GetJobBillMapAsync()
    {
        var rows = await _db.Bills
            .Where(b => b.JobOrderId != null)
            .Select(b => new { JobId = b.JobOrderId!.Value, b.Status, b.IsIssued })
            .ToListAsync();

        return rows
            .GroupBy(r => r.JobId)
            .ToDictionary(
                g => g.Key,
                g => new JobBillInfo(
                    g.Max(r => r.Status),        // BillStatus is an ordered enum (Draft<…<Closed)
                    g.Any(r => r.IsIssued)));
    }

    /// <summary>
    /// Prepares (does not persist) a Draft bill pre-linked to a JobOrder. Only the
    /// FK columns that live on <see cref="Bill"/> are copied — Shipper / Consignee /
    /// Commodity / Port etc. are read through the <c>JobOrder</c> navigation, never duplicated.
    /// </summary>
    public static Bill PrepareForJob(JobOrder job, BillMode mode) => new()
    {
        Mode            = mode,
        JobOrderId      = job.Id,
        JobOrder        = job,
        BranchId        = job.BranchId,
        BillingClientId = job.BillingClientId,
        CurrencyId      = job.CurrencyId,
        BillDate        = DateTime.UtcNow.Date,
        ExchangeRate    = 1m,
    };

    /// <summary>Maps a JobOrder's mode to the bill mode it should raise.</summary>
    public static BillMode BillModeFor(JobMode mode) =>
        mode == JobMode.Forwarding ? BillMode.Forwarding : BillMode.Clearance;

    /// <summary>
    /// The auto-billing hook: keeps a job's linked bills in sync. Every Clearance/Forwarding job
    /// owns one primary bill (CB / FB). A job that also carries a transport leg
    /// (<see cref="JobOrder.RequiresTransportation"/>) owns a second, Transportation (TB) bill linked
    /// to the same JobId — created the first time the flag is set and refreshed thereafter. Both bills
    /// go through the SAME idempotent per-mode upsert, so there is exactly one copy of the billing
    /// logic. Called from the JobOrder workflow inside its save transaction (no nested transaction)
    /// and from <see cref="BillingSyncService"/> for backfill.
    /// </summary>
    public async Task UpsertForJobAsync(JobOrder job, string? actor = null)
    {
        // Batch/one-time callers (BillingSyncService, startup backfill) pass an explicit actor so
        // no HTTP/circuit auth context is required; the live workflow leaves it null → resolves the user.
        var user = actor ?? await CurrentUserAsync();

        // Primary operational bill for the job's mode (Clearance → CB, Forwarding → FB).
        await UpsertBillForModeAsync(job, BillModeFor(job.Mode), user);

        // Transportation is optional — only when the job declares a transport leg. Same code path,
        // keyed on JobId + TB mode, so it never duplicates and coexists with the primary bill.
        if (job.RequiresTransportation)
            await UpsertBillForModeAsync(job, BillMode.Transportation, user);
    }

    /// <summary>
    /// Idempotent create-or-update of the single bill for a given job + mode. Copies only the FK header
    /// columns (Branch / Customer / Currency); everything else (Shipper, Consignee, Commodity, Ports,
    /// Container, No, Date, Type) is read through the <c>Bill.JobOrder</c> navigation — never duplicated.
    /// Re-running for the same job+mode updates the existing bill instead of creating a duplicate.
    /// </summary>
    private async Task<Bill> UpsertBillForModeAsync(JobOrder job, BillMode mode, string user)
    {
        var existing = await _db.Bills
            .Where(b => b.JobOrderId == job.Id && b.Mode == mode)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            var bill = PrepareForJob(job, mode);
            bill.JobOrder = null;                       // link by FK only (parent already tracked)
            bill.BillDate = job.JobOrderDate;           // mirror the job date
            bill.FinYear  = ComputeFinYear(bill.BillDate);
            bill.BillNo   = await NextBillNoAsync(_db, mode, bill.FinYear);
            bill.Status   = BillStatus.Draft;
            bill.CreatedBy = user;
            bill.CreatedOn = DateTime.UtcNow;
            RecalcTotals(bill);                         // zeroes for an empty bill
            _db.Bills.Add(bill);
            await _db.SaveChangesAsync();
            await LogEventAsync(bill.Id, BillEventType.Created, $"Auto-created from job {job.JobOrderNo}.", user);
            return bill;
        }

        // Refresh only the copied FK header fields; keep charges / status / number / totals.
        existing.BranchId        = job.BranchId;
        existing.BillingClientId = job.BillingClientId;
        existing.CurrencyId      = job.CurrencyId;
        existing.ModifiedBy      = user;
        existing.ModifiedOn      = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(existing.Id, BillEventType.Updated, $"Synced from job {job.JobOrderNo}.", user);
        return existing;
    }

    /// <summary>Removes every bill linked to a job that is still in Draft (used when a Draft job is deleted).</summary>
    public async Task RemoveDraftBillsForJobAsync(long jobOrderId)
    {
        var draftBills = await _db.Bills
            .Where(b => b.JobOrderId == jobOrderId && b.Status == BillStatus.Draft)
            .ToListAsync();
        if (draftBills.Count > 0)
        {
            _db.Bills.RemoveRange(draftBills);   // BillCharges/BillEvents cascade
            await _db.SaveChangesAsync();
        }
    }

    public Task<Bill?> GetByIdAsync(long id) =>
        WithRefs()
            .Include(b => b.Charges).ThenInclude(c => c.ChargeCode)
            .Include(b => b.Charges).ThenInclude(c => c.Sac)
            .Include(b => b.Events)
            .FirstOrDefaultAsync(b => b.Id == id);

    // ── Numbering ────────────────────────────────────────────────────────────
    public static int ComputeFinYear(DateTime d) => d.Month >= 4 ? d.Year : d.Year - 1;

    private static string Prefix(BillMode mode) => mode switch
    {
        BillMode.Clearance      => "CB",
        BillMode.Forwarding     => "FB",
        BillMode.Transportation => "TB",
        _ => "BL",
    };

    // Static so both the auto-billing path (UpsertForJobAsync) and the BillWorkflowHandler share one
    // copy of the numbering rule (per Mode, per FY).
    public static async Task<string> NextBillNoAsync(AppDbContext db, BillMode mode, int finYear)
    {
        var prefix    = Prefix(mode);
        var fyDisplay = $"{(finYear % 100):D2}-{((finYear + 1) % 100):D2}";

        var lastNo = await db.Bills
            .Where(b => b.Mode == mode && b.FinYear == finYear)
            .OrderByDescending(b => b.Id)
            .Select(b => b.BillNo)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo))
        {
            var tail = lastNo.Split('/').Last();
            if (int.TryParse(tail, out var n)) seq = n + 1;
        }
        return $"{prefix}/{fyDisplay}/{seq:D4}";
    }

    // ── Totals ───────────────────────────────────────────────────────────────
    public static void RecalcTotals(Bill bill)
    {
        decimal sub = 0, gst = 0, total = 0;
        foreach (var c in bill.Charges)
        {
            // Discount lines reduce the bill; every other category adds to it.
            var sign = c.Category == ChargeCategory.Discount ? -1m : 1m;
            c.Amount    = decimal.Round(c.Quantity * c.Rate, 2);
            c.GstAmount = decimal.Round(c.Amount * c.GstRate / 100m, 2);
            c.NetAmount = c.Amount + c.GstAmount;
            sub   += sign * c.Amount;
            gst   += sign * c.GstAmount;
            total += sign * c.NetAmount;
        }
        bill.SubTotal    = sub;
        bill.GstAmount   = gst;
        bill.TotalAmount = total;
    }

    // ── Add / Edit / Delete — routed through the Workflow Engine ───────────────
    // Numbering, totals, charge replacement and the timeline event live in BillWorkflowHandler.
    public async Task<Bill> CreateAsync(Bill bill)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Create, await CurrentUserAsync(), bill, _handler);
        await _orchestrator.RunAsync(ctx);
        return bill;
    }

    public async Task UpdateAsync(Bill bill)
    {
        var ctx = new WorkflowContext(WorkflowOperationType.Update, await CurrentUserAsync(), bill, _handler);
        await _orchestrator.RunAsync(ctx);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Guards a bill from moving forward (Submit / Approve) when it has nothing to bill — no charge rows
    /// or a non-positive total. A zero-value bill must never reach Accounts (its approval would post an
    /// empty voucher). Applies to every bill mode (Clearance / Forwarding / Transportation). Throws a
    /// user-friendly message the pages surface as a toast.
    /// </summary>
    private async Task EnsureBillableAsync(Bill b, string action)
    {
        var hasCharges = await _db.BillCharges.AnyAsync(c => c.BillId == b.Id);
        if (!hasCharges || b.TotalAmount <= 0)
            throw new InvalidOperationException(
                $"Bill {b.BillNo} cannot be {action}: it has no charges (total is {b.TotalAmount:N2}). " +
                "Add at least one charge with a positive amount first — a zero-value bill cannot post to Accounts.");
    }

    public async Task SubmitAsync(long id)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Draft && b.Status != BillStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit a bill in '{b.Status}' status.");
        await EnsureBillableAsync(b, "submitted");

        b.Status      = BillStatus.Submitted;
        b.SubmittedBy = user;
        b.SubmittedOn = DateTime.UtcNow;
        b.RejectedBy  = null; b.RejectedOn = null; b.RejectionReason = null;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Submitted, "Submitted for verification.", user);
        await _wflog.LogTransitionAsync("Billing", "Bill", id, b.BillNo, WorkflowOperationType.Submit, user, $"{b.Mode} bill {b.BillNo} submitted for verification.");
        _dash.NotifyChanged();
    }

    public async Task VerifyAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted bills can be verified (current: {b.Status}).");

        b.Status     = BillStatus.Verified;
        b.VerifiedBy = user;
        b.VerifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Verified, note, user);
        await _wflog.LogTransitionAsync("Billing", "Bill", id, b.BillNo, WorkflowOperationType.Verify, user, $"{b.Mode} bill {b.BillNo} verified; awaiting approval.");
        _dash.NotifyChanged();
    }

    public async Task ApproveAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Verified)
            throw new InvalidOperationException($"Only Verified bills can be approved (current: {b.Status}).");
        await EnsureBillableAsync(b, "approved");   // never post accounting for a zero-value bill

        // Approving a bill posts its balanced double-entry to Accounts automatically (rule #4). Status +
        // event + voucher commit together or roll back together (rule #11).
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            b.Status     = BillStatus.Approved;
            b.ApprovedBy = user;
            b.ApprovedOn = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogEventAsync(id, BillEventType.Approved, note, user);

            await _accounting.PostForBillAsync(id, user);   // Dr A/R, Cr Revenue, Cr GST
            await _wflog.LogTransitionAsync("Billing", "Bill", id, b.BillNo, WorkflowOperationType.Approve, user, $"{b.Mode} bill {b.BillNo} approved; voucher posted to Accounts.", notifyManagers: true);
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
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Submitted && b.Status != BillStatus.Verified)
            throw new InvalidOperationException($"Cannot reject a bill in '{b.Status}' status.");

        b.Status          = BillStatus.Rejected;
        b.RejectedBy      = user;
        b.RejectedOn      = DateTime.UtcNow;
        b.RejectionReason = reason;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Rejected, reason, user);
        await _wflog.LogTransitionAsync("Billing", "Bill", id, b.BillNo, WorkflowOperationType.Reject, user, $"{b.Mode} bill {b.BillNo} rejected: {reason}", notifyManagers: true);
        _dash.NotifyChanged();
    }

    public async Task CloseAsync(long id, string? note = null)
    {
        var user = await CurrentUserAsync();
        var b = await _db.Bills.FindAsync(id) ?? throw new KeyNotFoundException();
        if (b.Status != BillStatus.Approved)
            throw new InvalidOperationException($"Only Approved bills can be closed (current: {b.Status}).");

        b.Status   = BillStatus.Closed;
        b.ClosedBy = user;
        b.ClosedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogEventAsync(id, BillEventType.Closed, note, user);
        await _wflog.LogTransitionAsync("Billing", "Bill", id, b.BillNo, WorkflowOperationType.Close, user, $"{b.Mode} bill {b.BillNo} closed.");
        _dash.NotifyChanged();
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var b = await _db.Bills.FindAsync(id);
        if (b is null) return false;

        var ctx = new WorkflowContext(WorkflowOperationType.Delete, await CurrentUserAsync(), b, _handler);
        await _orchestrator.RunAsync(ctx);   // handler blocks non-Draft deletes
        return true;
    }

    // ── Events ───────────────────────────────────────────────────────────────
    private async Task LogEventAsync(long billId, BillEventType type, string? notes, string actor)
    {
        _db.BillEvents.Add(new BillEvent
        {
            BillId    = billId,
            EventType = type,
            Notes     = notes,
            Actor     = actor,
            At        = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
