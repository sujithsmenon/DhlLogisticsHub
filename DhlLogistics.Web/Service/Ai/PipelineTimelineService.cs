namespace DhlLogistics.Web.Service.Ai;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>One chronological event in a business reference's timeline.</summary>
public record TimelineEntry(DateTime At, string Category, string Title, string? Detail);

/// <summary>Everything known about one DHL Invoice Number (master business reference).</summary>
public record PipelineTimeline(string Reference, List<TimelineEntry> Entries)
{
    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>
/// AI Email Automation — Phase 7. Read-only aggregator: given the DHL Invoice
/// Number (the master business reference), it gathers the original email +
/// attachments, AI extraction, both approvals, the shipment, job, bills,
/// customer invoices, accounting vouchers, notifications and workflow audit into
/// one chronological timeline. Pure reads — no writes, no existing logic touched.
/// </summary>
public class PipelineTimelineService
{
    private readonly IDbContextFactory<AppDbContext> _dbf;

    public PipelineTimelineService(IDbContextFactory<AppDbContext> dbf) => _dbf = dbf;

    /// <summary>Find master references (draft / job / bill) matching a search term.</summary>
    public async Task<List<string>> SearchReferencesAsync(string? term, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        var t = (term ?? string.Empty).Trim();

        var fromDrafts = db.ShipmentDraftApprovals.AsNoTracking()
            .Where(a => a.DhlInvoiceNumber != null && a.DhlInvoiceNumber != "")
            .Select(a => a.DhlInvoiceNumber!);
        var fromJobs = db.JobOrders.AsNoTracking()
            .Where(j => j.CustomerInvoiceNumber != "")
            .Select(j => j.CustomerInvoiceNumber);
        var fromBills = db.Bills.AsNoTracking()
            .Where(b => b.CustomerInvoiceNumber != null && b.CustomerInvoiceNumber != "")
            .Select(b => b.CustomerInvoiceNumber!);

        var all = await fromDrafts.Concat(fromJobs).Concat(fromBills)
            .Where(r => t == "" || r.ToLower().Contains(t.ToLower()))
            .Distinct()
            .OrderBy(r => r)
            .Take(50)
            .ToListAsync(ct);
        return all;
    }

    public async Task<PipelineTimeline> BuildAsync(string reference, CancellationToken ct = default)
    {
        var entries = new List<TimelineEntry>();
        var refv = (reference ?? string.Empty).Trim();
        if (refv.Length == 0) return new PipelineTimeline(refv, entries);

        await using var db = await _dbf.CreateDbContextAsync(ct);

        // 1. Draft approvals (AI extraction + first approval) keyed by the master reference.
        var drafts = await db.ShipmentDraftApprovals.AsNoTracking()
            .Where(a => a.DhlInvoiceNumber == refv).ToListAsync(ct);
        var draftIds = drafts.Select(d => d.Id).ToList();
        var emailIds = drafts.Select(d => d.IncomingEmailId).Distinct().ToList();

        // 2. Original emails + attachments.
        var emails = await db.IncomingEmails.AsNoTracking()
            .Where(e => emailIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Subject, e.From, e.ReceivedDate,
                Attachments = e.Attachments.Select(a => new { a.FileName, a.SizeBytes }).ToList() })
            .ToListAsync(ct);
        foreach (var e in emails)
        {
            entries.Add(new TimelineEntry(e.ReceivedDate, "Email",
                string.IsNullOrWhiteSpace(e.Subject) ? "(no subject)" : e.Subject, $"From {e.From}"));
            foreach (var a in e.Attachments)
                entries.Add(new TimelineEntry(e.ReceivedDate, "Attachment", a.FileName,
                    $"{a.SizeBytes:N0} bytes"));
        }

        foreach (var d in drafts)
        {
            entries.Add(new TimelineEntry(d.CreatedAt, "AI Extraction",
                $"{d.ShipmentType} {d.Direction} extracted".Trim(),
                $"{d.Confidence:P0} confidence via {d.Provider}"));
            if (d.ReviewedAt is not null)
                entries.Add(new TimelineEntry(d.ReviewedAt.Value, "Approval #1 (Draft)",
                    d.Status, $"by {d.ReviewedBy}{(d.ReviewNotes is null ? "" : $" — {d.ReviewNotes}")}"));
        }

        // 3. Created shipments (AWB / Export) from the drafts.
        foreach (var d in drafts.Where(x => x.CreatedShipmentId is not null))
        {
            if (d.CreatedShipmentType == "Awb")
            {
                var s = await db.AwbShipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.CreatedShipmentId, ct);
                if (s is not null)
                    entries.Add(new TimelineEntry(d.ShipmentCreatedAt ?? s.ReceivedAt, "Shipment",
                        $"AWB {s.HawbNo}", $"{s.OriginStation} → {s.DestinationStation} · {s.Status}"));
            }
            else if (d.CreatedShipmentType == "Export")
            {
                var s = await db.ExportJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.CreatedShipmentId, ct);
                if (s is not null)
                    entries.Add(new TimelineEntry(d.ShipmentCreatedAt ?? s.ReceivedAt, "Shipment",
                        $"Sea Job {s.JobReference}", $"{s.CustomerName} · {s.Status}"));
            }
        }

        // 4. Second approval (shipment → job).
        var jobApprovals = await db.ShipmentJobApprovals.AsNoTracking()
            .Where(a => draftIds.Contains(a.ShipmentDraftApprovalId)).ToListAsync(ct);
        foreach (var a in jobApprovals)
        {
            entries.Add(new TimelineEntry(a.CreatedAt, "Approval #2 (Job)",
                $"Proposed {a.ProposedMode} job", $"awaiting approval · {a.ShipmentKind} #{a.ShipmentId}"));
            if (a.ReviewedAt is not null)
                entries.Add(new TimelineEntry(a.ReviewedAt.Value, "Approval #2 (Job)",
                    a.Status, $"by {a.ReviewedBy}{(a.ReviewNotes is null ? "" : $" — {a.ReviewNotes}")}"));
        }

        // 5. JobOrders + events (existing pipeline) by CustomerInvoiceNumber.
        var jobs = await db.JobOrders.AsNoTracking()
            .Where(j => j.CustomerInvoiceNumber == refv).ToListAsync(ct);
        var jobNos = jobs.Select(j => j.JobOrderNo).Where(n => n != "").ToList();
        foreach (var j in jobs)
        {
            entries.Add(new TimelineEntry(j.JobOrderDate, "Job",
                $"{j.Mode} job {j.JobOrderNo}", $"{j.ShipmentMode} {j.ShipmentType} · {j.Status}"));
            var events = await db.JobOrderEvents.AsNoTracking()
                .Where(e => e.JobOrderId == j.Id).ToListAsync(ct);
            foreach (var ev in events)
                entries.Add(new TimelineEntry(ev.At, "Job", $"{ev.EventType}", ev.Notes ?? ev.Actor));
        }

        // 6. Bills + events by CustomerInvoiceNumber.
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.CustomerInvoiceNumber == refv).ToListAsync(ct);
        var billNos = bills.Select(b => b.BillNo).Where(n => n != "").ToList();
        var billIds = bills.Select(b => b.Id).ToList();
        foreach (var b in bills)
            entries.Add(new TimelineEntry(b.BillDate, "Bill", $"Bill {b.BillNo}",
                $"{b.ShipmentTypeName ?? "charges"} · {b.Status}"));
        var billEvents = await db.BillEvents.AsNoTracking()
            .Where(e => billIds.Contains(e.BillId)).ToListAsync(ct);
        foreach (var ev in billEvents)
            entries.Add(new TimelineEntry(ev.At, "Bill", ev.EventType.ToString(), ev.Notes ?? ev.Actor));

        // 7. Customer invoices by CustomerInvoiceNumber.
        var invoices = await db.CustomerInvoices.AsNoTracking()
            .Where(i => i.CustomerInvoiceNumber == refv).ToListAsync(ct);
        foreach (var i in invoices)
            entries.Add(new TimelineEntry(i.InvoiceDate, "Invoice", $"Invoice {i.InvoiceNo}",
                $"{i.TotalAmount:N2} (incl. GST {i.GstAmount:N2}) · {i.Status}"));

        // 8. Accounting vouchers referencing a related bill/job.
        var refKeys = billNos.Concat(jobNos).Append(refv).ToList();
        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(v => v.ReferenceNo != null && refKeys.Contains(v.ReferenceNo)).ToListAsync(ct);
        foreach (var v in vouchers)
            entries.Add(new TimelineEntry(v.VoucherDate, "Accounting",
                $"{v.Type} voucher {v.VoucherNo}", $"{v.Narration} · {v.Status}"));

        // 9. Workflow audit for the related job/bill entities.
        var auditRefs = jobNos.Concat(billNos).ToList();
        if (auditRefs.Count > 0)
        {
            var audit = await db.WorkflowAuditLogs.AsNoTracking()
                .Where(l => l.EntityRef != null && auditRefs.Contains(l.EntityRef)).ToListAsync(ct);
            foreach (var l in audit)
                entries.Add(new TimelineEntry(l.At, "Audit", l.Summary, $"{l.Module} · {l.Actor}"));
        }

        // 10. Notifications tagged with this reference.
        var notes = await db.Notifications.AsNoTracking()
            .Where(n => n.JobCode == refv).ToListAsync(ct);
        foreach (var n in notes)
            entries.Add(new TimelineEntry(n.CreatedAt, "Notification", n.Title, n.Body));

        return new PipelineTimeline(refv, entries.OrderBy(e => e.At).ToList());
    }
}
