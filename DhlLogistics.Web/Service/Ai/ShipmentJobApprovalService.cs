namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>Outcome of creating a Clearing/Forwarding job from an approved shipment.</summary>
public record JobCreationResult(bool Success, long? JobOrderId, string? JobOrderNo, string Message)
{
    public static JobCreationResult Fail(string msg) => new(false, null, null, msg);
}

/// <summary>
/// AI Email Automation — Phase 5. The second approval gate. After Phase 4 creates
/// a shipment, this queues a <see cref="ShipmentJobApproval"/>; once a human
/// approves it, a Clearing or Forwarding <see cref="JobOrder"/> is created by
/// REUSING <see cref="JobOrderService.CreateAsync"/> (the existing workflow owns
/// numbering, validation, save, billing, timeline). No job logic is duplicated
/// and nothing is created without this approval.
/// </summary>
public class ShipmentJobApprovalService
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly JobOrderService _jobs;
    private readonly ILogger<ShipmentJobApprovalService> _log;
    private readonly NotificationService? _notify;

    public ShipmentJobApprovalService(
        IDbContextFactory<AppDbContext> dbf,
        JobOrderService jobs,
        ILogger<ShipmentJobApprovalService> log,
        NotificationService? notify = null)
    {
        _dbf = dbf;
        _jobs = jobs;
        _log = log;
        _notify = notify;
    }

    // ── Queue (called after Phase 4 shipment creation) ───────────────────────

    public async Task<ShipmentJobApproval?> QueueAsync(
        ShipmentDraftApproval draft, string shipmentKind, int shipmentId, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);

        // De-dup: one open second-approval per created shipment.
        var existing = await db.ShipmentJobApprovals
            .FirstOrDefaultAsync(a => a.ShipmentKind == shipmentKind
                                   && a.ShipmentId == shipmentId
                                   && a.Status != DraftApprovalStatus.Rejected, ct);
        if (existing is not null) return existing;

        var direction = DefaultDirection(draft.Direction);
        var approval = new ShipmentJobApproval
        {
            ShipmentDraftApprovalId = draft.Id,
            ShipmentKind      = shipmentKind,
            ShipmentId        = shipmentId,
            DhlInvoiceNumber  = draft.DhlInvoiceNumber,
            CustomerName      = draft.Customer,
            EmailSubject      = draft.EmailSubject,
            ProposedMode      = DefaultMode(direction),
            ShipmentMode      = DefaultShipmentMode(draft.ShipmentType),
            ShipmentDirection = direction,
            Status            = DraftApprovalStatus.Pending,
        };
        db.ShipmentJobApprovals.Add(approval);
        await db.SaveChangesAsync(ct);

        await NotifyAsync(approval);
        return approval;
    }

    // ── Approve → create JobOrder ────────────────────────────────────────────

    public async Task<JobCreationResult> ApproveAsync(
        int id, string reviewer, JobMode mode, int billingClientId,
        string? customerInvoiceNumber, CancellationToken ct = default)
    {
        ShipmentJobApproval a;
        await using (var db = await _dbf.CreateDbContextAsync(ct))
        {
            var found = await db.ShipmentJobApprovals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null) return JobCreationResult.Fail("Approval not found.");
            if (found.Status != DraftApprovalStatus.Pending) return JobCreationResult.Fail("Already reviewed.");
            a = found;
        }

        var clientId = billingClientId > 0
            ? billingClientId
            : await ResolveOrCreateClientAsync(a.CustomerName, ct);
        if (clientId <= 0)
            return JobCreationResult.Fail("Select a billing client (customer could not be resolved).");

        var invoice = FirstNonBlank(customerInvoiceNumber, a.DhlInvoiceNumber, $"AUTO-{a.Id}");
        var job = BuildJobOrder(a, mode, clientId, invoice, JobOrderService.ComputeFinYear(DateTime.UtcNow));

        JobOrder created;
        try
        {
            created = await _jobs.CreateAsync(job);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "JobOrder creation failed for second-approval {Id}.", id);
            return JobCreationResult.Fail($"JobOrder not created: {ex.Message}");
        }

        await using (var db = await _dbf.CreateDbContextAsync(ct))
        {
            var found = await db.ShipmentJobApprovals.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is not null && found.Status == DraftApprovalStatus.Pending)
            {
                found.Status = DraftApprovalStatus.Approved;
                found.ReviewedBy = reviewer;
                found.ReviewedAt = DateTime.UtcNow;
                found.CreatedJobOrderId = created.Id;
                await db.SaveChangesAsync(ct);
            }
        }

        _log.LogInformation("Second-approval {Id} -> {Mode} JobOrder {JobNo} ({JobId}).",
            id, mode, created.JobOrderNo, created.Id);
        return new JobCreationResult(true, created.Id, created.JobOrderNo,
            $"{mode} job {created.JobOrderNo} created.");
    }

    public async Task<bool> RejectAsync(int id, string reviewer, string reason, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        var a = await db.ShipmentJobApprovals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null || a.Status != DraftApprovalStatus.Pending) return false;
        a.Status = DraftApprovalStatus.Rejected;
        a.ReviewedBy = reviewer;
        a.ReviewedAt = DateTime.UtcNow;
        a.ReviewNotes = reason;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── Queries + client resolution ──────────────────────────────────────────

    public async Task<List<ShipmentJobApproval>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        return await db.ShipmentJobApprovals.AsNoTracking()
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ShipmentJobApproval?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        return await db.ShipmentJobApprovals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<List<DhlClient>> GetClientsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        return await db.Clients.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync(ct);
    }

    /// <summary>Find a client by company name (case-insensitive); create a minimal one if absent.</summary>
    public async Task<int> ResolveOrCreateClientAsync(string? name, CancellationToken ct = default)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return 0;

        await using var db = await _dbf.CreateDbContextAsync(ct);
        var existing = await db.Clients
            .FirstOrDefaultAsync(c => c.CompanyName.ToLower() == trimmed.ToLower(), ct);
        if (existing is not null) return existing.Id;

        var client = new DhlClient { CompanyName = trimmed };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return client.Id;
    }

    // ── Pure decision + mapping (unit-testable) ──────────────────────────────

    public static JobShipmentType DefaultDirection(string? direction) =>
        string.Equals(direction, "Export", StringComparison.OrdinalIgnoreCase)
            ? JobShipmentType.Export : JobShipmentType.Import;

    /// <summary>Import → Clearance (customs), Export → Forwarding.</summary>
    public static JobMode DefaultMode(JobShipmentType dir) =>
        dir == JobShipmentType.Export ? JobMode.Forwarding : JobMode.Clearance;

    public static JobShipmentMode DefaultShipmentMode(string? shipmentType) =>
        string.Equals(shipmentType, "Air", StringComparison.OrdinalIgnoreCase)
            ? JobShipmentMode.Air : JobShipmentMode.Sea;

    public static JobOrder BuildJobOrder(
        ShipmentJobApproval a, JobMode mode, int clientId, string customerInvoice, int finYear) => new()
    {
        Mode                  = mode,
        ShipmentMode          = a.ShipmentMode,
        ShipmentType          = a.ShipmentDirection,
        CargoType             = a.ShipmentMode == JobShipmentMode.Air ? JobCargoType.Air : JobCargoType.FCL,
        CustomerInvoiceNumber = customerInvoice,
        BillingClientId       = clientId,
        ShipperId             = clientId,
        ConsigneeId           = clientId,
        FinYear               = finYear,
        JobOrderDate          = DateTime.UtcNow.Date,
    };

    private static string FirstNonBlank(params string?[] vals) =>
        vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private async Task NotifyAsync(ShipmentJobApproval a)
    {
        if (_notify is null) return;
        try
        {
            await _notify.NotifyManagersAsync(
                title:   "Shipment awaiting job approval",
                body:    $"{a.ShipmentKind} #{a.ShipmentId} · {a.DhlInvoiceNumber ?? a.EmailSubject} · propose {a.ProposedMode} job",
                type:    "JobApproval",
                jobId:   a.Id,
                jobCode: a.DhlInvoiceNumber);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Second-approval {Id} queued but notification failed.", a.Id);
        }
    }
}
