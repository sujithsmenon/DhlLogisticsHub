namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public enum ShipmentKind { Unknown, Awb, Export }

/// <summary>Outcome of creating a shipment from an approved draft.</summary>
public record ShipmentCreationResult(bool Success, ShipmentKind Kind, int? ShipmentId, string Message)
{
    public static ShipmentCreationResult Fail(string msg) => new(false, ShipmentKind.Unknown, null, msg);
}

/// <summary>
/// AI Email Automation — Phase 4. Turns an APPROVED <see cref="ShipmentDraftApproval"/>
/// into a real shipment by REUSING the existing services:
///   Import Air → <see cref="AwbShipmentService.CreateManualAsync"/>
///   Export Sea → <see cref="ExportJobService.CreateAsync"/>
/// No shipment business logic is duplicated — the workflow orchestrator inside
/// those services still owns numbering, save, billing, timeline, etc.
///
/// Idempotent: a draft that already has <see cref="ShipmentDraftApproval.CreatedShipmentId"/>
/// is never created twice. Only ACTS on approved drafts — nothing bypasses approval.
/// </summary>
public class ShipmentFromDraftService
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly AwbShipmentService _awb;
    private readonly ExportJobService _export;
    private readonly ShipmentJobApprovalService _jobApprovals;
    private readonly ILogger<ShipmentFromDraftService> _log;

    public ShipmentFromDraftService(
        IDbContextFactory<AppDbContext> dbf,
        AwbShipmentService awb,
        ExportJobService export,
        ShipmentJobApprovalService jobApprovals,
        ILogger<ShipmentFromDraftService> log)
    {
        _dbf = dbf;
        _awb = awb;
        _export = export;
        _jobApprovals = jobApprovals;
        _log = log;
    }

    public async Task<ShipmentCreationResult> CreateFromApprovedDraftAsync(int approvalId, CancellationToken ct = default)
    {
        ShipmentDraftApproval draft;
        string? sourceEmail;
        await using (var db = await _dbf.CreateDbContextAsync(ct))
        {
            var a = await db.ShipmentDraftApprovals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == approvalId, ct);
            if (a is null) return ShipmentCreationResult.Fail("Draft not found.");
            if (a.Status != DraftApprovalStatus.Approved)
                return ShipmentCreationResult.Fail("Draft is not approved.");
            if (a.CreatedShipmentId is not null)
                return new ShipmentCreationResult(true,
                    a.CreatedShipmentType == "Awb" ? ShipmentKind.Awb : ShipmentKind.Export,
                    a.CreatedShipmentId, "Shipment already created.");
            draft = a;
            sourceEmail = await db.IncomingEmails.Where(e => e.Id == a.IncomingEmailId)
                .Select(e => e.From).FirstOrDefaultAsync(ct);
        }

        var kind = ResolveKind(draft);
        int shipmentId;
        switch (kind)
        {
            case ShipmentKind.Awb:
                var awb = await _awb.CreateManualAsync(MapToAwb(draft, sourceEmail));
                shipmentId = awb.Id;
                break;
            case ShipmentKind.Export:
                var job = await _export.CreateAsync(MapToExport(draft, sourceEmail));
                shipmentId = job.Id;
                break;
            default:
                return ShipmentCreationResult.Fail(
                    "Cannot determine shipment kind — set Shipment Type (Air/Sea) on the draft.");
        }

        // Record the link back on the draft (idempotency + timeline).
        await using (var db = await _dbf.CreateDbContextAsync(ct))
        {
            var a = await db.ShipmentDraftApprovals.FirstOrDefaultAsync(x => x.Id == approvalId, ct);
            if (a is not null && a.CreatedShipmentId is null)
            {
                a.CreatedShipmentType = kind == ShipmentKind.Awb ? "Awb" : "Export";
                a.CreatedShipmentId = shipmentId;
                a.ShipmentCreatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        _log.LogInformation("Draft {Id} -> {Kind} shipment {ShipmentId} created.", approvalId, kind, shipmentId);

        // Phase 5: queue the SECOND approval on the created shipment (best-effort;
        // never fails the shipment creation).
        try
        {
            await _jobApprovals.QueueAsync(draft, kind == ShipmentKind.Awb ? "Awb" : "Export", shipmentId, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Shipment {ShipmentId} created but second-approval queue failed.", shipmentId);
        }

        return new ShipmentCreationResult(true, kind, shipmentId, $"{kind} shipment #{shipmentId} created.");
    }

    // ── Pure decision + mapping (unit-testable, no side effects) ──────────────

    /// <summary>Import Air → AWB; Export Sea → Export job. Falls back to any
    /// unambiguous type-only signal when direction is missing.</summary>
    public static ShipmentKind ResolveKind(ShipmentDraftApproval d)
    {
        var type = d.ShipmentType?.Trim();
        if (string.Equals(type, "Air", StringComparison.OrdinalIgnoreCase)) return ShipmentKind.Awb;
        if (string.Equals(type, "Sea", StringComparison.OrdinalIgnoreCase)) return ShipmentKind.Export;
        return ShipmentKind.Unknown;
    }

    public static AwbShipment MapToAwb(ShipmentDraftApproval d, string? sourceEmail) => new()
    {
        HawbNo             = FirstNonBlank(d.Hawb, d.Mawb, $"AUTO-{d.DhlInvoiceNumber ?? d.Id.ToString()}"),
        ConsigneeName      = d.Customer ?? string.Empty,        // import: customer is the consignee
        OriginStation      = d.OriginPort ?? string.Empty,
        DestinationStation = d.DestinationPort ?? string.Empty,
        ReferenceNumbers   = JoinRefs(
            ("DHL Invoice", d.DhlInvoiceNumber), ("MAWB", d.Mawb),
            ("BL", d.BlNumber), ("Ref", d.ReferenceNumbers)),
        SourceEmail        = sourceEmail ?? string.Empty,
    };

    public static ExportJob MapToExport(ShipmentDraftApproval d, string? sourceEmail) => new()
    {
        JobReference    = FirstNonBlank(d.DhlInvoiceNumber, d.ReferenceNumbers, $"AUTO-{d.Id}"),
        CustomerName    = FirstNonBlank(d.Customer, "Unknown (from email)"),
        CargoDescription = d.ReferenceNumbers ?? string.Empty,
        Notes           = JoinRefs(
            ("DHL Invoice", d.DhlInvoiceNumber), ("BL", d.BlNumber),
            ("Container", d.ContainerNumber), ("POL", d.OriginPort),
            ("POD", d.DestinationPort),
            ("ETD", d.Etd?.ToString("dd-MMM-yyyy")), ("ETA", d.Eta?.ToString("dd-MMM-yyyy")),
            ("From", sourceEmail)),
    };

    private static string FirstNonBlank(params string?[] vals) =>
        vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string JoinRefs(params (string Label, string? Value)[] parts) =>
        string.Join(" · ", parts
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Label}: {p.Value!.Trim()}"));
}
