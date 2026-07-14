namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

// ── Contracts ────────────────────────────────────────────────────────────────

/// <summary>One document inside a Billing Group, flattened to what a linked-documents panel / search hit
/// needs. Deliberately a projection, not an entity: the panel must never re-load or re-save business rows.</summary>
public sealed record BillingGroupDoc(
    string   Kind,        // "Job" | "Clearance Bill" | "Forwarding Bill" | "Transportation Bill" | "Customer Invoice"
    long     Id,
    string   Number,      // JobOrderNo / BillNo / CI InvoiceNo
    DateTime Date,
    string?  Client,
    string?  Branch,
    string   Status,
    decimal? Amount,
    decimal? Gst,
    string?  Remarks,
    string   Route);      // page this document opens on

/// <summary>Everything sharing one CustomerInvoiceNumber. The group is <b>virtual</b> — nothing is stored to
/// create it; it is derived on read from the reference the documents already carry.</summary>
public sealed record BillingGroup(
    string                  CustomerInvoiceNumber,
    List<BillingGroupDoc>   Jobs,
    List<BillingGroupDoc>   ClearanceBills,        // Clearance + Forwarding (the "C&F" side)
    List<BillingGroupDoc>   TransportationBills,
    BillingGroupDoc?        CustomerInvoice)
{
    public IEnumerable<BillingGroupDoc> AllBills => ClearanceBills.Concat(TransportationBills);
    public bool IsEmpty => Jobs.Count == 0 && ClearanceBills.Count == 0 && TransportationBills.Count == 0;
}

/// <summary>
/// Lets a future module contribute its own documents to a Billing Group without this service (or any
/// existing caller) being changed — register an implementation in DI and it is picked up automatically.
/// </summary>
public interface IBillingGroupContributor
{
    /// <summary>Section label for the contributed documents (e.g. "Warehouse Orders").</summary>
    string Section { get; }

    /// <summary>Documents in this module carrying the given CustomerInvoiceNumber. Return empty when none.</summary>
    Task<List<BillingGroupDoc>> GetAsync(AppDbContext db, string customerInvoiceNumber, CancellationToken ct = default);
}

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Resolves the <b>Billing Group</b>: every document sharing one <see cref="Bill.CustomerInvoiceNumber"/>.
///
/// <para>The group is virtual and requires no manual linking — Job, Clearance/Forwarding Bill and
/// Transportation Bill already carry the reference (a job-raised bill inherits it in
/// <see cref="BillService.PrepareForJob"/>), so <b>existing records participate automatically</b> with no
/// migration or re-save. Records with no CustomerInvoiceNumber simply form no group and behave exactly as
/// before.</para>
///
/// <para>Read-only. This service owns no billing logic: it queries and projects, and never writes.</para>
/// </summary>
public class BillingGroupService
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IBillingGroupContributor> _contributors;

    public BillingGroupService(AppDbContext db, IEnumerable<IBillingGroupContributor> contributors)
    {
        _db = db;
        _contributors = contributors;
    }

    /// <summary>Normalised group key. Empty/whitespace never forms a group — otherwise every legacy record
    /// with a blank reference would collapse into one giant bogus group.</summary>
    private static string? Key(string? customerInvoiceNumber)
    {
        var k = customerInvoiceNumber?.Trim();
        return string.IsNullOrEmpty(k) ? null : k;
    }

    // ── The five documented entry points ─────────────────────────────────────

    /// <summary>
    /// Every operational record in the group. All four modules now share the CustomerInvoiceNumber model, so
    /// this returns Clearance/Forwarding JobOrders <i>and</i> Export Jobs <i>and</i> AWB Shipments — a group
    /// raised from an AWB is as real as one raised from a JobOrder.
    /// </summary>
    public async Task<List<BillingGroupDoc>> GetLinkedJobsAsync(string? cinv, CancellationToken ct = default)
    {
        var key = Key(cinv);
        if (key is null) return new();
        var lower = key.ToLower();

        var jobs = await _db.JobOrders.AsNoTracking()
            .Where(j => j.CustomerInvoiceNumber != null && j.CustomerInvoiceNumber.ToLower() == lower)
            .OrderBy(j => j.Id)
            .Select(j => new BillingGroupDoc(
                "Job", j.Id, j.JobOrderNo, j.JobOrderDate,
                j.BillingClient!.CompanyName, j.Branch!.BranchName,
                j.Status.ToString(), null, null, j.Remarks,
                j.Mode == JobMode.Forwarding ? "/jobs/forwarding" : "/jobs/clearance"))
            .ToListAsync(ct);

        var exports = await _db.ExportJobs.AsNoTracking()
            .Where(e => e.CustomerInvoiceNumber != null && e.CustomerInvoiceNumber.ToLower() == lower)
            .OrderBy(e => e.Id)
            .Select(e => new BillingGroupDoc(
                "Export Job", e.Id, e.JobReference, e.ReceivedAt,
                e.CustomerName, null, e.Status.ToString(), null, null, e.Notes, "/export"))
            .ToListAsync(ct);

        var awbs = await _db.AwbShipments.AsNoTracking()
            .Where(a => a.CustomerInvoiceNumber != null && a.CustomerInvoiceNumber.ToLower() == lower)
            .OrderBy(a => a.Id)
            .Select(a => new BillingGroupDoc(
                "AWB Shipment", a.Id, a.HawbNo, a.ReceivedAt,
                a.ConsigneeName, null, a.Status.ToString(), null, null, a.HandlingInfo, "/awb"))
            .ToListAsync(ct);

        jobs.AddRange(exports);
        jobs.AddRange(awbs);
        return jobs;
    }

    /// <summary>Clearance <i>and</i> Forwarding bills — the "Clearance &amp; Forwarding" side of the group.</summary>
    public Task<List<BillingGroupDoc>> GetClearanceBillsAsync(string? cinv, CancellationToken ct = default) =>
        BillsAsync(cinv, transportation: false, ct);

    public Task<List<BillingGroupDoc>> GetTransportationBillsAsync(string? cinv, CancellationToken ct = default) =>
        BillsAsync(cinv, transportation: true, ct);

    /// <summary>The consolidated invoice raised over this group, if one has been generated yet.</summary>
    public async Task<BillingGroupDoc?> GetCustomerInvoiceAsync(string? cinv, CancellationToken ct = default)
    {
        var key = Key(cinv);
        if (key is null) return null;

        return await _db.CustomerInvoices.AsNoTracking()
            .Where(i => i.CustomerInvoiceNumber.ToLower() == key.ToLower()
                     && i.Status != CustomerInvoiceStatus.Cancelled)
            .OrderByDescending(i => i.Id)
            .Select(i => new BillingGroupDoc(
                "Customer Invoice", i.Id, i.InvoiceNo, i.InvoiceDate,
                i.BillingClient!.CompanyName, i.Branch!.BranchName,
                i.Status.ToString(), i.TotalAmount, i.GstAmount, i.Remarks,
                "/bills/customer-invoices"))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The whole group in one round of queries — what the Linked Documents panel binds to.</summary>
    public async Task<BillingGroup> GetBillingGroupAsync(string? cinv, CancellationToken ct = default)
    {
        var key = Key(cinv);
        if (key is null) return new BillingGroup(string.Empty, new(), new(), new(), null);

        var jobs      = await GetLinkedJobsAsync(key, ct);
        var clearance = await GetClearanceBillsAsync(key, ct);
        var transport = await GetTransportationBillsAsync(key, ct);
        var invoice   = await GetCustomerInvoiceAsync(key, ct);

        // Future modules join the group here without this file changing.
        foreach (var c in _contributors)
            clearance.AddRange(await c.GetAsync(_db, key, ct));

        return new BillingGroup(key, jobs, clearance, transport, invoice);
    }

    // ── Resolve the group from any member document ───────────────────────────

    /// <summary>Group that the given Bill belongs to (searching a Bill No must surface the sibling bills).</summary>
    public async Task<BillingGroup> GetGroupForBillAsync(long billId, CancellationToken ct = default)
    {
        var cinv = await _db.Bills.AsNoTracking()
            .Where(b => b.Id == billId).Select(b => b.CustomerInvoiceNumber).FirstOrDefaultAsync(ct);
        return await GetBillingGroupAsync(cinv, ct);
    }

    /// <summary>Group that the given Job belongs to (searching a Job No must surface its bills + invoice).</summary>
    public async Task<BillingGroup> GetGroupForJobAsync(long jobId, CancellationToken ct = default)
    {
        var cinv = await _db.JobOrders.AsNoTracking()
            .Where(j => j.Id == jobId).Select(j => j.CustomerInvoiceNumber).FirstOrDefaultAsync(ct);
        return await GetBillingGroupAsync(cinv, ct);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task<List<BillingGroupDoc>> BillsAsync(string? cinv, bool transportation, CancellationToken ct)
    {
        var key = Key(cinv);
        if (key is null) return new();

        var q = _db.Bills.AsNoTracking()
            .Where(b => b.CustomerInvoiceNumber != null && b.CustomerInvoiceNumber.ToLower() == key.ToLower());

        q = transportation
            ? q.Where(b => b.Mode == BillMode.Transportation)
            : q.Where(b => b.Mode != BillMode.Transportation);

        return await q.OrderBy(b => b.Id)
            .Select(b => new BillingGroupDoc(
                b.Mode == BillMode.Transportation ? "Transportation Bill"
                    : b.Mode == BillMode.Forwarding ? "Forwarding Bill" : "Clearance Bill",
                b.Id, b.BillNo, b.BillDate,
                b.BillingClient!.CompanyName, b.Branch!.BranchName,
                b.Status.ToString(), b.TotalAmount, b.GstAmount, b.Remarks,
                b.Mode == BillMode.Transportation ? "/bills/transportation"
                    : b.Mode == BillMode.Forwarding ? "/bills/forwarding" : "/bills/clearance"))
            .ToListAsync(ct);
    }
}
