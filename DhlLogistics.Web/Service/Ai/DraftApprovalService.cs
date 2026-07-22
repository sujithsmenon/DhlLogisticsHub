namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// AI Email Automation — Phase 3. Turns an incoming email's AI extraction into
/// a persisted <see cref="ShipmentDraftApproval"/> and manages its review
/// lifecycle (Approve / Reject / Edit). Notifies managers (Web + Android +
/// iPhone) via the existing <see cref="NotificationService"/>.
///
/// Guarantee: this service NEVER creates a shipment/job. Nothing bypasses the
/// human approval that Phase 4 will act on.
/// </summary>
public class DraftApprovalService
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly EmailAiReaderService _reader;
    private readonly NotificationService? _notify;
    private readonly ILogger<DraftApprovalService> _log;
    private readonly double _threshold;

    public DraftApprovalService(
        IDbContextFactory<AppDbContext> dbf,
        EmailAiReaderService reader,
        IConfiguration config,
        ILogger<DraftApprovalService> log,
        NotificationService? notify = null)
    {
        _dbf = dbf;
        _reader = reader;
        _notify = notify;
        _log = log;
        _threshold = double.TryParse(config["AiSettings:ApprovalConfidenceThreshold"], out var t) ? t : 0.5;
    }

    // ── Creation ─────────────────────────────────────────────────────────────

    /// <summary>Run the AI reader on the email, then queue a draft approval.</summary>
    public async Task<ShipmentDraftApproval?> CreateFromEmailAsync(int emailId, CancellationToken ct = default)
    {
        EmailContent? content;
        string subject;
        await using (var db = await _dbf.CreateDbContextAsync(ct))
        {
            var e = await db.IncomingEmails
                .Where(x => x.Id == emailId)
                .Select(x => new
                {
                    x.Subject, x.TextBody, x.HtmlBody,
                    Names = x.Attachments.Select(a => a.FileName).ToList()
                })
                .FirstOrDefaultAsync(ct);
            if (e is null) return null;
            subject = e.Subject;
            content = new EmailContent(e.Subject, e.TextBody, e.HtmlBody, e.Names);
        }

        var draft = await _reader.ReadAsync(content, ct);
        return await CreateAsync(emailId, subject, draft, ct);
    }

    /// <summary>Queue a draft approval from an already-computed draft (no duplicate AI call).</summary>
    public async Task<ShipmentDraftApproval?> CreateAsync(
        int emailId, string emailSubject, ShipmentDraft draft, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);

        // De-dup: one open (Pending/Approved) draft per email.
        var existing = await db.ShipmentDraftApprovals
            .Where(a => a.IncomingEmailId == emailId && a.Status != DraftApprovalStatus.Rejected)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var approval = new ShipmentDraftApproval
        {
            IncomingEmailId  = emailId,
            EmailSubject     = emailSubject,
            ShipmentType     = draft.ShipmentType,
            Direction        = draft.Direction,
            Customer         = draft.Customer,
            DhlInvoiceNumber = draft.DhlInvoiceNumber,
            ContainerNumber  = draft.ContainerNumber,
            Hawb             = draft.Hawb,
            Mawb             = draft.Mawb,
            BlNumber         = draft.BlNumber,
            OriginPort       = draft.OriginPort,
            DestinationPort  = draft.DestinationPort,
            Eta              = draft.Eta,
            Etd              = draft.Etd,
            ReferenceNumbers = draft.ReferenceNumbers,
            Confidence       = draft.Confidence,
            Provider         = draft.Provider,
            HighConfidence   = draft.Confidence >= _threshold,
            ExtractionNotes  = draft.Notes.Count > 0 ? string.Join(" | ", draft.Notes) : null,
            Status           = DraftApprovalStatus.Pending,
        };

        db.ShipmentDraftApprovals.Add(approval);

        var email = await db.IncomingEmails.FindAsync(new object[] { emailId }, ct);
        if (email is not null) email.ProcessingStatus = EmailProcessingStatus.DraftCreated;

        await db.SaveChangesAsync(ct);

        await NotifyAsync(approval);
        return approval;
    }

    // ── Review lifecycle ─────────────────────────────────────────────────────

    /// <summary>Persist approver edits while the draft is still Pending.</summary>
    public Task<bool> SaveEditsAsync(ShipmentDraftApproval edited, CancellationToken ct = default) =>
        MutateAsync(edited.Id, DraftApprovalStatus.Pending, a => CopyEditable(edited, a), ct);

    /// <summary>Approve the draft (persisting any final edits). No shipment is created here.</summary>
    public Task<bool> ApproveAsync(ShipmentDraftApproval edited, string reviewer, CancellationToken ct = default) =>
        MutateAsync(edited.Id, DraftApprovalStatus.Pending, a =>
        {
            CopyEditable(edited, a);
            a.Status     = DraftApprovalStatus.Approved;
            a.ReviewedBy = reviewer;
            a.ReviewedAt = DateTime.UtcNow;
        }, ct, EmailProcessingStatus.Approved);

    /// <summary>Reject the draft with a reason.</summary>
    public Task<bool> RejectAsync(int id, string reviewer, string reason, CancellationToken ct = default) =>
        MutateAsync(id, DraftApprovalStatus.Pending, a =>
        {
            a.Status      = DraftApprovalStatus.Rejected;
            a.ReviewedBy  = reviewer;
            a.ReviewedAt  = DateTime.UtcNow;
            a.ReviewNotes = reason;
        }, ct, EmailProcessingStatus.Rejected);

    /// <summary>Shared "load Pending → mutate → save (+ optional email status)" helper.</summary>
    private async Task<bool> MutateAsync(
        int id, string requiredStatus, Action<ShipmentDraftApproval> mutate,
        CancellationToken ct, string? emailStatus = null)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        var a = await db.ShipmentDraftApprovals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null || a.Status != requiredStatus) return false;

        mutate(a);

        if (emailStatus is not null)
        {
            var email = await db.IncomingEmails.FindAsync(new object[] { a.IncomingEmailId }, ct);
            if (email is not null) email.ProcessingStatus = emailStatus;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void CopyEditable(ShipmentDraftApproval src, ShipmentDraftApproval dst)
    {
        dst.ShipmentType     = src.ShipmentType;
        dst.Direction        = src.Direction;
        dst.Customer         = src.Customer;
        dst.DhlInvoiceNumber = src.DhlInvoiceNumber;
        dst.ContainerNumber  = src.ContainerNumber;
        dst.Hawb             = src.Hawb;
        dst.Mawb             = src.Mawb;
        dst.BlNumber         = src.BlNumber;
        dst.OriginPort       = src.OriginPort;
        dst.DestinationPort  = src.DestinationPort;
        dst.Eta              = src.Eta;
        dst.Etd              = src.Etd;
        dst.ReferenceNumbers = src.ReferenceNumbers;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<ShipmentDraftApproval>> GetByStatusAsync(
        string status, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        return await db.ShipmentDraftApprovals.AsNoTracking()
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ShipmentDraftApproval?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        return await db.ShipmentDraftApprovals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    // ── Notification (best-effort; never blocks the pipeline) ────────────────

    private async Task NotifyAsync(ShipmentDraftApproval a)
    {
        if (_notify is null) return;
        try
        {
            var conf = $"{a.Confidence:P0}";
            var what = $"{a.ShipmentType ?? "Shipment"} {a.Direction}".Trim();
            await _notify.NotifyManagersAsync(
                title:   "New shipment draft awaiting approval",
                body:    $"{what} · {a.DhlInvoiceNumber ?? a.EmailSubject} · {conf} confidence",
                type:    "DraftApproval",
                jobId:   a.Id,
                jobCode: a.DhlInvoiceNumber);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Draft approval {Id} created but manager notification failed.", a.Id);
        }
    }
}
