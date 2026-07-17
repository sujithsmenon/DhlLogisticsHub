namespace DhlLogistics.Web.Service;

using System.Text.Json;
using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

// ── Contracts ────────────────────────────────────────────────────────────────

/// <summary>How seriously a repeated operational record should be taken.</summary>
public enum DuplicateSeverity
{
    /// <summary>Expected: the same record billed once per bill TYPE (e.g. a job billed on a Forwarding bill
    /// AND on a Transportation bill). Different bill types bill different things — not double billing.</summary>
    Information = 1,

    /// <summary>Potential duplicate billing: the same record appears on MORE THAN ONE bill of the SAME type
    /// (e.g. one job on two Forwarding bills). Advisory only — never blocks.</summary>
    Warning = 2,

    /// <summary>Reserved (red). Nothing raises this yet.</summary>
    Critical = 3,
}

/// <summary>One bill an operational record turned up on.</summary>
public sealed record DuplicateOccurrence(long BillId, string BillNo, BillMode BillType, decimal Amount);

/// <summary>One operational record that appears on more than one selected bill.</summary>
public sealed record DuplicateFinding(
    string SourceKey,                      // "JOB:52" — the authoritative comparison key (an ID, never a bill no)
    string RecordLabel,                    // "Forwarding Job FWD/26-27/0009"
    IReadOnlyList<DuplicateOccurrence> Occurrences,
    DuplicateSeverity Severity,
    string Reason)
{
    /// <summary>For a Warning: the redundant amount — every occurrence within an over-billed type beyond the
    /// first. This is the money genuinely at risk of being billed twice. Zero for Information findings.</summary>
    public decimal PotentialDuplicateAmount => Severity != DuplicateSeverity.Warning
        ? 0m
        : Occurrences.GroupBy(o => o.BillType)
                     .Where(g => g.Count() > 1)
                     .SelectMany(g => g.OrderBy(o => o.BillId).Skip(1))   // keep the first, the rest are redundant
                     .Sum(o => o.Amount);
}

/// <summary>The full advisory report for one selection.</summary>
public sealed record DuplicateReport(IReadOnlyList<DuplicateFinding> Findings)
{
    public IEnumerable<DuplicateFinding> Expected => Findings.Where(f => f.Severity == DuplicateSeverity.Information);
    public IEnumerable<DuplicateFinding> Warnings => Findings.Where(f => f.Severity >= DuplicateSeverity.Warning);

    public int ExpectedCount => Expected.Count();
    public int WarningCount  => Warnings.Count();

    /// <summary>Distinct bills carrying at least one WARNING-level record — what the popup highlights amber.</summary>
    public HashSet<long> WarningBillIds =>
        Warnings.SelectMany(f => f.Occurrences).Select(o => o.BillId).ToHashSet();

    /// <summary>Distinct bills carrying only INFORMATION-level records — highlighted blue.</summary>
    public HashSet<long> InfoBillIds
    {
        get
        {
            var warn = WarningBillIds;
            return Expected.SelectMany(f => f.Occurrences).Select(o => o.BillId)
                           .Where(id => !warn.Contains(id)).ToHashSet();
        }
    }

    public int     PotentialDuplicateBills  => WarningBillIds.Count;
    public decimal PotentialDuplicateAmount => Warnings.Sum(f => f.PotentialDuplicateAmount);

    /// <summary>Anything at all to show the user before generating?</summary>
    public bool HasFindings => Findings.Count > 0;

    /// <summary>Does this selection warrant the amber warning dialog (as opposed to pure information)?</summary>
    public bool HasWarnings => WarningCount > 0;
}

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Advisory duplicate-billing detection for a customer-invoice selection.
///
/// <para><b>Compares operational-record IDs, never bill numbers.</b> Two bills raised from the same job are a
/// duplicate of that JOB, regardless of what the bills are called. Keys come from
/// <see cref="BillingGroupService.GetSourceKeysAsync"/>, which projects ids only in a single query — so this
/// stays fast on a large Billing Group and loads no related entities.</para>
///
/// <para><b>Expected vs. true duplicate.</b> The same record billed once per bill TYPE is normal — a job is
/// legitimately billed on a Clearance/Forwarding bill AND on a Transportation bill, because those bill
/// different things. That is Information. Only the same record on MORE THAN ONE bill of the SAME type is a
/// potential duplicate, and that is a Warning.</para>
///
/// <para><b>Advisory only.</b> This service never modifies, removes or reorders a Bill. It answers a
/// question; the user's selection stays authoritative.</para>
/// </summary>
public class DuplicateBillingService
{
    private readonly AppDbContext _db;
    private readonly BillingGroupService _groups;
    private readonly AuthenticationStateProvider _auth;
    private readonly ILogger<DuplicateBillingService> _log;

    public DuplicateBillingService(AppDbContext db, BillingGroupService groups,
                                   AuthenticationStateProvider auth, ILogger<DuplicateBillingService> log)
    {
        _db = db;
        _groups = groups;
        _auth = auth;
        _log = log;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _auth.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    /// <summary>
    /// Analyses the selected bills. Returns an empty report when no operational record repeats.
    /// Two queries total, both id-projections — no entity graphs loaded.
    /// </summary>
    public async Task<DuplicateReport> AnalyseAsync(IReadOnlyCollection<long> billIds,
                                                    CancellationToken ct = default)
    {
        if (billIds is null || billIds.Count < 2) return new DuplicateReport(Array.Empty<DuplicateFinding>());

        // billId → "JOB:52" / "AWB:4" / "EXP:7". Standalone bills have no key and cannot duplicate anything.
        var keys = await _groups.GetSourceKeysAsync(billIds, ct);
        if (keys.Count == 0) return new DuplicateReport(Array.Empty<DuplicateFinding>());

        var bills = await _db.Bills.AsNoTracking()
            .Where(b => billIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BillNo, b.Mode, b.TotalAmount })
            .ToListAsync(ct);

        var findings = new List<DuplicateFinding>();

        foreach (var grp in keys.GroupBy(kv => kv.Value))          // group the SELECTED bills by source record
        {
            var billIdsForKey = grp.Select(kv => kv.Key).ToHashSet();
            if (billIdsForKey.Count < 2) continue;                  // appears once → not a duplicate at all

            var occurrences = bills
                .Where(b => billIdsForKey.Contains(b.Id))
                .Select(b => new DuplicateOccurrence(b.Id, b.BillNo, b.Mode, b.TotalAmount))
                .OrderBy(o => o.BillType).ThenBy(o => o.BillId)
                .ToList();

            // THE RULE: repeats WITHIN one bill type are suspicious; one-per-type is expected.
            var overBilledTypes = occurrences.GroupBy(o => o.BillType).Where(g => g.Count() > 1).ToList();

            var severity = overBilledTypes.Count > 0 ? DuplicateSeverity.Warning : DuplicateSeverity.Information;

            var reason = severity == DuplicateSeverity.Warning
                ? $"Appears on {string.Join(" and ", overBilledTypes.Select(g => $"{g.Count()} {g.Key} bills"))} "
                  + "— the same record billed more than once under the same bill type."
                : "Expected (Different Bill Types) — billed once per bill type, which bill different charges.";

            findings.Add(new DuplicateFinding(
                grp.Key, await LabelAsync(grp.Key, ct), occurrences, severity, reason));
        }

        // Warnings first — the dialog leads with what matters.
        findings = findings.OrderByDescending(f => f.Severity).ThenBy(f => f.SourceKey).ToList();
        return new DuplicateReport(findings);
    }

    /// <summary>
    /// Records the user's override when they choose "Generate Anyway". Reuses the existing WorkflowAuditLog
    /// (Kind = Audit) — no new audit store. Written only AFTER the invoice is successfully raised, so the log
    /// can name it.
    /// </summary>
    public async Task LogOverrideAsync(CustomerInvoice invoice, DuplicateReport report,
                                       IReadOnlyCollection<long> selectedBillIds, string? actor = null)
    {
        if (!report.HasFindings) return;

        var user = actor ?? await CurrentUserAsync();

        var bills = await _db.Bills.AsNoTracking()
            .Where(b => selectedBillIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BillNo, b.Mode, b.TotalAmount })
            .ToListAsync();

        var details = JsonSerializer.Serialize(new
        {
            CustomerInvoiceNumber = invoice.CustomerInvoiceNumber,
            InvoiceNo             = invoice.InvoiceNo,
            SelectedBills         = bills.Select(b => new { b.Id, b.BillNo, Type = b.Mode.ToString(), b.TotalAmount }),
            DuplicateRecords      = report.Findings.Select(f => new
            {
                OperationalRecord = f.SourceKey,
                Record            = f.RecordLabel,
                Severity          = f.Severity.ToString(),
                f.Reason,
                AppearsIn         = f.Occurrences.Select(o => new { o.BillNo, Type = o.BillType.ToString(), o.Amount }),
                f.PotentialDuplicateAmount,
            }),
            report.ExpectedCount,
            report.WarningCount,
            report.PotentialDuplicateAmount,
            Reason = "User confirmed duplicate billing warning.",
        }, new JsonSerializerOptions { WriteIndented = false });

        _db.WorkflowAuditLogs.Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Audit,
            Module     = "Billing",
            EntityType = "CustomerInvoice",
            EntityId   = invoice.Id,
            EntityRef  = invoice.InvoiceNo,
            Operation  = WorkflowOperationType.Create,
            Summary    = $"User confirmed duplicate billing warning — invoice {invoice.InvoiceNo} generated over "
                       + $"{selectedBillIds.Count} bill(s) with {report.WarningCount} potential duplicate(s).",
            Details    = details,
            Actor      = user,
            At         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        _log.LogWarning("Duplicate billing override: {User} generated {InvoiceNo} despite {Warnings} warning(s) "
                      + "({Amount:N2} potentially duplicated).",
                        user, invoice.InvoiceNo, report.WarningCount, report.PotentialDuplicateAmount);
    }

    /// <summary>Human label for an operational record key ("JOB:52" → "Forwarding Job FWD/26-27/0009").</summary>
    private async Task<string> LabelAsync(string sourceKey, CancellationToken ct)
    {
        var parts = sourceKey.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var id)) return sourceKey;

        switch (parts[0])
        {
            case "JOB":
                var j = await _db.JobOrders.AsNoTracking().Where(x => x.Id == id)
                    .Select(x => new { x.JobOrderNo, x.Mode }).FirstOrDefaultAsync(ct);
                return j is null ? sourceKey
                    : $"{(j.Mode == JobMode.Forwarding ? "Forwarding" : "Clearance")} Job {j.JobOrderNo}";
            case "AWB":
                var a = await _db.AwbShipments.AsNoTracking().Where(x => x.Id == (int)id)
                    .Select(x => x.HawbNo).FirstOrDefaultAsync(ct);
                return a is null ? sourceKey : $"AWB Shipment {a}";
            case "EXP":
                var e = await _db.ExportJobs.AsNoTracking().Where(x => x.Id == (int)id)
                    .Select(x => x.JobReference).FirstOrDefaultAsync(ct);
                return e is null ? sourceKey : $"Sea Shipment {e}";
            default:
                return sourceKey;
        }
    }
}
