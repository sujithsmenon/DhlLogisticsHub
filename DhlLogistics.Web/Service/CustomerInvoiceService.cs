namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>Filter state of the Customer Invoice list. Every field is applied server-side.</summary>
public sealed class CustomerInvoiceFilter
{
    public int?      BillingClientId { get; set; }
    public int?      BranchId        { get; set; }
    public CustomerInvoiceStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To   { get; set; }

    public string? CustomerInvoiceNumber { get; set; }
    public string? SystemInvoiceNo       { get; set; }
    public string? BillNo      { get; set; }
    public string? JobNo       { get; set; }
    public string? AwbNo       { get; set; }
    public string? ContainerNo { get; set; }
    public string? VehicleNo   { get; set; }

    /// <summary>Global free-text search across all of the above.</summary>
    public string? Text { get; set; }
}

/// <summary>One row of the Customer Invoice list — a flat projection, never an entity graph.</summary>
public sealed record CustomerInvoiceRow(
    long      Id,
    string    CustomerInvoiceNumber,
    string    InvoiceNo,
    string?   Customer,
    string?   Branch,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    CustomerInvoiceStatus Status,
    int       BillCount,
    int       JobCount,
    decimal   SubTotal,
    decimal   GstAmount,
    decimal   TotalAmount,
    string?   CreatedBy,
    DateTime  CreatedOn);

/// <summary>One line of the read-only invoice timeline.</summary>
public sealed record InvoiceTimelineEntry(string Event, string? Detail, string Actor, DateTime At);

/// <summary>One row of the Service Breakdown (presentation summary — never an accounting figure).</summary>
public sealed record ServiceBreakdownRow(string Category, decimal Total);

/// <summary>
/// Lifecycle of the consolidated <see cref="CustomerInvoice"/> raised over a Billing Group (the Bills sharing
/// one CustomerInvoiceNumber).
///
/// <para><b>Not a second InvoiceService.</b> The charge/GST/total maths stays in
/// <see cref="BillService.RecalcTotals"/> (this service only SUMS already-computed bill totals), the PDF stays
/// in <see cref="InvoiceService"/> (Phase 2 calls into it), and accounting stays in
/// <see cref="AccountingService"/> — untouched, because A/R is posted per Bill at <b>approval</b>, never at
/// invoice issue. Consolidating N bills onto one PDF therefore cannot double-post the ledger.</para>
///
/// <para>What is genuinely new here and lives nowhere else: the independent CI number sequence, the
/// same-group validation, and the double-invoice guard.</para>
/// </summary>
public class CustomerInvoiceService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _auth;
    private readonly ILogger<CustomerInvoiceService> _log;

    public CustomerInvoiceService(AppDbContext db, AuthenticationStateProvider auth,
                                  ILogger<CustomerInvoiceService> log)
    {
        _db = db;
        _auth = auth;
        _log = log;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _auth.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── List / filter (server-side paging: the grid never pulls the whole table) ──

    /// <summary>
    /// One page of customer invoices, filtered server-side. Every filter — including the ones that reach
    /// THROUGH the invoice into its bills and their operational records (bill no, job no, AWB, container,
    /// vehicle) — is translated into SQL, so the grid never materialises rows it will not show.
    /// Counts and totals come from projections, not from loading the bill graphs (no N+1).
    /// </summary>
    public async Task<(List<CustomerInvoiceRow> Rows, int Total)> SearchAsync(
        CustomerInvoiceFilter f, int skip, int take, CancellationToken ct = default)
    {
        IQueryable<CustomerInvoice> q = _db.CustomerInvoices.AsNoTracking();

        if (f.BillingClientId is { } cid) q = q.Where(i => i.BillingClientId == cid);
        if (f.BranchId is { } bid)        q = q.Where(i => i.BranchId == bid);
        if (f.Status is { } st)           q = q.Where(i => i.Status == st);
        if (f.From is { } from)           q = q.Where(i => i.InvoiceDate >= from.Date);
        if (f.To is { } to)               q = q.Where(i => i.InvoiceDate <= to.Date);

        if (!string.IsNullOrWhiteSpace(f.CustomerInvoiceNumber))
            q = q.Where(i => EF.Functions.ILike(i.CustomerInvoiceNumber, $"%{f.CustomerInvoiceNumber}%"));
        if (!string.IsNullOrWhiteSpace(f.SystemInvoiceNo))
            q = q.Where(i => EF.Functions.ILike(i.InvoiceNo, $"%{f.SystemInvoiceNo}%"));

        // Filters that reach through to the included bills and their sources.
        if (!string.IsNullOrWhiteSpace(f.BillNo))
            q = q.Where(i => i.Bills.Any(b => EF.Functions.ILike(b.BillNo, $"%{f.BillNo}%")));
        if (!string.IsNullOrWhiteSpace(f.ContainerNo))
            q = q.Where(i => i.Bills.Any(b => b.ContainerNumber != null && EF.Functions.ILike(b.ContainerNumber, $"%{f.ContainerNo}%")));
        if (!string.IsNullOrWhiteSpace(f.VehicleNo))
            q = q.Where(i => i.Bills.Any(b => b.VehicleNumber != null && EF.Functions.ILike(b.VehicleNumber, $"%{f.VehicleNo}%")));
        if (!string.IsNullOrWhiteSpace(f.AwbNo))
            q = q.Where(i => i.Bills.Any(b => b.AwbOrBlNumber != null && EF.Functions.ILike(b.AwbOrBlNumber, $"%{f.AwbNo}%")));
        if (!string.IsNullOrWhiteSpace(f.JobNo))
            q = q.Where(i => i.Bills.Any(b => b.JobOrder != null && EF.Functions.ILike(b.JobOrder.JobOrderNo, $"%{f.JobNo}%")));

        // Free-text global search across everything above.
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var like = $"%{f.Text}%";
            q = q.Where(i =>
                   EF.Functions.ILike(i.CustomerInvoiceNumber, like)
                || EF.Functions.ILike(i.InvoiceNo, like)
                || EF.Functions.ILike(i.BillingClient!.CompanyName, like)
                || i.Bills.Any(b => EF.Functions.ILike(b.BillNo, like)
                                 || (b.ContainerNumber != null && EF.Functions.ILike(b.ContainerNumber, like))
                                 || (b.VehicleNumber   != null && EF.Functions.ILike(b.VehicleNumber, like))
                                 || (b.AwbOrBlNumber   != null && EF.Functions.ILike(b.AwbOrBlNumber, like))
                                 || (b.JobOrder != null && EF.Functions.ILike(b.JobOrder.JobOrderNo, like))));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(i => i.Id)
            .Skip(skip).Take(take)
            .Select(i => new CustomerInvoiceRow(
                i.Id,
                i.CustomerInvoiceNumber,
                i.InvoiceNo,
                i.BillingClient!.CompanyName,
                i.Branch!.BranchName,
                i.Bills.Min(b => (DateTime?)b.BillDate),
                i.Bills.Max(b => (DateTime?)b.BillDate),
                i.Status,
                i.Bills.Count,
                // distinct originating records behind the invoice — counted in SQL, no graph loaded
                i.Bills.Select(b => b.JobOrderId).Distinct().Count(x => x != null),
                i.SubTotal, i.GstAmount, i.TotalAmount,
                i.CreatedBy, i.CreatedOn))
            .ToListAsync(ct);

        return (rows, total);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public Task<CustomerInvoice?> GetAsync(long id) =>
        _db.CustomerInvoices
            .Include(i => i.BillingClient).Include(i => i.Branch).Include(i => i.Currency)
            .Include(i => i.Bills)
            .FirstOrDefaultAsync(i => i.Id == id);

    /// <summary>
    /// The Bills that MAY be consolidated for a given customer reference — i.e. what the Generate popup lists.
    /// A bill qualifies when it is Approved (or Closed), carries this exact reference, and is not ALREADY on a
    /// consolidated invoice. Excluded here rather than filtered in the UI, so the rule has exactly one home.
    ///
    /// <para>A bill that was previously issued the per-bill way IS still selectable: consolidating it
    /// SUPERSEDES that individual invoice (see <see cref="GenerateAsync"/>) rather than double-billing the
    /// customer. Without this, every bill already issued individually would be permanently ineligible and the
    /// popup would be empty for all existing data.</para>
    /// </summary>
    public async Task<List<Bill>> GetSelectableBillsAsync(string customerInvoiceNumber, long? includeInvoiceId = null)
    {
        var key = (customerInvoiceNumber ?? string.Empty).Trim();
        if (key.Length == 0) return new();

        return await _db.Bills.AsNoTracking()
            .Include(b => b.BillingClient).Include(b => b.Branch).Include(b => b.Currency)
            .Where(b => b.CustomerInvoiceNumber != null
                     && b.CustomerInvoiceNumber.ToLower() == key.ToLower()
                     && (b.Status == BillStatus.Approved || b.Status == BillStatus.Closed)
                     // The ONE hard block: already consolidated onto another customer invoice.
                     && (b.CustomerInvoiceId == null || b.CustomerInvoiceId == includeInvoiceId))
            .OrderBy(b => b.Mode).ThenBy(b => b.Id)
            .ToListAsync();
    }

    // ── Generate ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Raises ONE consolidated invoice over the selected bills. Validates the whole Billing Group invariant
    /// before writing anything, then links the bills to the new invoice inside a single transaction.
    /// Does not generate the PDF — that is <see cref="InvoiceService"/>'s job (Phase 2).
    /// </summary>
    public async Task<CustomerInvoice> GenerateAsync(IReadOnlyCollection<long> billIds,
                                                     string? paymentTerms = null,
                                                     string? remarks = null,
                                                     string? actor = null)
    {
        if (billIds is null || billIds.Count == 0)
            throw new InvalidOperationException("Select at least one bill to invoice.");

        var bills = await _db.Bills
            .Include(b => b.Charges)
            .Where(b => billIds.Contains(b.Id))
            .ToListAsync();

        if (bills.Count != billIds.Count)
            throw new InvalidOperationException("One or more selected bills no longer exist.");

        // ── Invariants. All checked BEFORE any write. ────────────────────────

        // 1. Only Bills — and only approved ones — may be invoiced.
        var notApproved = bills.Where(b => b.Status is not (BillStatus.Approved or BillStatus.Closed)).ToList();
        if (notApproved.Count > 0)
            throw new InvalidOperationException(
                $"Only approved bills can be invoiced. Not approved: {string.Join(", ", notApproved.Select(b => b.BillNo))}.");

        // 2. Never mix Billing Groups: every bill must carry the SAME non-empty CustomerInvoiceNumber.
        var refs = bills.Select(b => (b.CustomerInvoiceNumber ?? string.Empty).Trim()).ToList();
        if (refs.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("Every selected bill must have a Customer Invoice Number.");
        var distinct = refs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count > 1)
            throw new InvalidOperationException(
                $"Bills from different Customer Invoice Numbers cannot be invoiced together ({string.Join(", ", distinct)}).");
        var cinv = distinct[0];

        // 3. Double-invoice guard — the whole reason Bill.CustomerInvoiceId exists. A bill may sit on at most
        //    ONE consolidated invoice, ever.
        var already = bills.Where(b => b.CustomerInvoiceId != null).ToList();
        if (already.Count > 0)
            throw new InvalidOperationException(
                $"Already on a customer invoice: {string.Join(", ", already.Select(b => b.BillNo))}.");

        // A bill previously issued the per-bill way is NOT rejected — its individual invoice is superseded
        // below. That keeps exactly one ACTIVE invoice per bill without double-billing the customer, and
        // without it every already-issued bill would be permanently un-consolidatable.
        var toSupersede = bills.Where(b => b.InvoiceStatus == InvoiceStatus.Issued).ToList();

        // 4. One invoice = one billing party (an invoice cannot be addressed to two clients).
        if (bills.Select(b => b.BillingClientId).Distinct().Count() > 1)
            throw new InvalidOperationException("All selected bills must belong to the same billing client.");

        var zero = bills.Where(b => b.TotalAmount <= 0).ToList();
        if (zero.Count > 0)
            throw new InvalidOperationException(
                $"Cannot invoice zero-value bills: {string.Join(", ", zero.Select(b => b.BillNo))}.");

        var user      = actor ?? await CurrentUserAsync();
        var anchor    = bills.OrderBy(b => b.Id).First();
        var invoiceDt = DateTime.UtcNow.Date;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var invoice = new CustomerInvoice
        {
            FinYear               = BillService.ComputeFinYear(invoiceDt),
            CustomerInvoiceNumber = cinv,
            InvoiceDate           = invoiceDt,
            BillingClientId       = anchor.BillingClientId,
            BranchId              = anchor.BranchId,
            CurrencyId            = anchor.CurrencyId,
            // Snapshot: the SUM of the bills' own totals. Never recomputed from charges here — that maths
            // has exactly one home (BillService.RecalcTotals) and it already ran when each bill was saved.
            SubTotal    = bills.Sum(b => b.SubTotal),
            GstAmount   = bills.Sum(b => b.GstAmount),
            TotalAmount = bills.Sum(b => b.TotalAmount),
            Status       = CustomerInvoiceStatus.Issued,
            PaymentTerms = paymentTerms,
            DueDate      = ComputeDueDate(invoiceDt, paymentTerms),
            Remarks      = remarks,
            CreatedBy    = user,
            CreatedOn    = DateTime.UtcNow,
        };
        invoice.InvoiceNo = await NextInvoiceNoAsync(_db, invoice.FinYear);

        _db.CustomerInvoices.Add(invoice);
        await _db.SaveChangesAsync();      // need the Id before linking the bills

        foreach (var b in bills)
        {
            b.CustomerInvoiceId = invoice.Id;   // the guard AND the supersede reference
            b.ModifiedOn = DateTime.UtcNow;
            b.ModifiedBy = user;
        }

        // ── Supersede the previously-issued individual invoices — NON-DESTRUCTIVELY. ──
        // Nothing is deleted or rewritten: the bill keeps its original InvoiceNumber / InvoiceDate, and its
        // original PDF row survives for audit (flagged inactive). The bill's InvoiceStatus becomes Superseded
        // and Bill.CustomerInvoiceId points at the consolidated invoice that replaced it, so reports, search
        // and the linked-documents panel can show BOTH and say which one is active.
        if (toSupersede.Count > 0)
        {
            var supersededIds = toSupersede.Select(b => b.Id).ToList();

            foreach (var b in toSupersede)
                b.InvoiceStatus = InvoiceStatus.Superseded;   // IsIssued/InvoiceNumber deliberately untouched

            // Retire the old customer-invoice PDFs: kept for audit, but no longer the active document.
            var oldDocs = await _db.InvoiceDocuments
                .Where(d => supersededIds.Contains(d.BillId)
                         && d.DocumentType == InvoiceDocumentType.CustomerInvoice
                         && d.IsActive)
                .ToListAsync();
            foreach (var d in oldDocs) d.IsActive = false;

            LogEvent(invoice, "Invoice Superseded",
                $"Superseded the individual invoice(s) of {string.Join(", ", toSupersede.Select(b => b.BillNo))}.", user);

            _log.LogInformation(
                "Customer invoice {InvoiceNo} SUPERSEDES {Count} individual invoice(s): {Bills}.",
                invoice.InvoiceNo, toSupersede.Count,
                string.Join(", ", toSupersede.Select(b => $"{b.BillNo}/{b.InvoiceNumber}")));
        }

        LogEvent(invoice, "Customer Invoice Created",
            $"{invoice.InvoiceNo} raised over {bills.Count} bill(s): {string.Join(", ", bills.Select(b => b.BillNo))}. "
          + $"Total {invoice.TotalAmount:N2}.", user);
        LogEvent(invoice, "Invoice Issued", $"Group {cinv}.", user);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogInformation("Customer invoice {InvoiceNo} raised over {Count} bill(s) for group {Group} by {User}.",
                            invoice.InvoiceNo, bills.Count, cinv, user);
        return invoice;
    }

    /// <summary>Cancels a consolidated invoice and RELEASES its bills (CustomerInvoiceId → null) so they can be
    /// invoiced again. Accounting is untouched: nothing was posted at issue, so nothing is reversed here.</summary>
    public async Task CancelAsync(long invoiceId, string? reason = null, string? actor = null)
    {
        var invoice = await _db.CustomerInvoices.Include(i => i.Bills)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new InvalidOperationException("Customer invoice not found.");

        if (invoice.Status == CustomerInvoiceStatus.Cancelled) return;   // idempotent

        var user = actor ?? await CurrentUserAsync();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var billIds = invoice.Bills.Select(b => b.Id).ToList();

        foreach (var b in invoice.Bills)
        {
            b.CustomerInvoiceId = null;      // released — free to be invoiced again

            // Un-supersede: a bill whose individual invoice this consolidated one had replaced gets that
            // invoice back as the active one. Otherwise cancelling would leave the bill with NO active
            // invoice at all, even though its original was never deleted.
            if (b.InvoiceStatus == InvoiceStatus.Superseded)
                b.InvoiceStatus = InvoiceStatus.Issued;
        }

        var revived = await _db.InvoiceDocuments
            .Where(d => billIds.Contains(d.BillId)
                     && d.DocumentType == InvoiceDocumentType.CustomerInvoice
                     && !d.IsActive
                     && d.CustomerInvoiceId == null)   // only the bill's OWN retired per-bill PDFs
            .ToListAsync();
        foreach (var d in revived) d.IsActive = true;

        invoice.Status             = CustomerInvoiceStatus.Cancelled;
        invoice.CancelledOn        = DateTime.UtcNow;
        invoice.CancelledBy        = user;
        invoice.CancellationReason = reason;

        LogEvent(invoice, "Invoice Cancelled",
            $"{invoice.Bills.Count} bill(s) released; their original invoices reactivated."
          + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason}"), user);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogInformation("Customer invoice {InvoiceNo} cancelled by {User}.", invoice.InvoiceNo, user);
    }

    // ── Timeline / breakdown / reopen ────────────────────────────────────────

    /// <summary>
    /// Read-only chronological timeline. Reuses the existing WorkflowAuditLog store (EntityType =
    /// "CustomerInvoice") — no separate event table, so no migration.
    /// </summary>
    public async Task<List<InvoiceTimelineEntry>> GetTimelineAsync(long invoiceId, CancellationToken ct = default)
    {
        var logs = await _db.WorkflowAuditLogs.AsNoTracking()
            .Where(l => l.EntityType == "CustomerInvoice" && l.EntityId == invoiceId)
            .OrderBy(l => l.At)
            .Select(l => new InvoiceTimelineEntry(l.Summary, l.Details, l.Actor, l.At))
            .ToListAsync(ct);
        return logs;
    }

    /// <summary>
    /// Service Breakdown for an invoice — charge totals per category. Reuses
    /// <see cref="InvoiceService.CategoryLabel"/>, the SAME mapping the PDF uses, so the page and the document
    /// can never disagree, and a future ChargeCategory appears in both with no code change.
    /// Presentation only: the totals sum to the invoice's own grand total; nothing is recomputed.
    /// </summary>
    public async Task<List<ServiceBreakdownRow>> GetServiceBreakdownAsync(long invoiceId, CancellationToken ct = default)
    {
        var charges = await _db.BillCharges.AsNoTracking()
            .Where(c => c.Bill!.CustomerInvoiceId == invoiceId)
            .Select(c => new { c.Category, c.NetAmount })
            .ToListAsync(ct);

        return charges
            .GroupBy(c => InvoiceService.CategoryLabel(c.Category))
            .Select(g => new ServiceBreakdownRow(g.Key, g.Sum(x => x.NetAmount)))
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    /// <summary>
    /// Reopens a cancelled invoice: re-attaches its bills and re-supersedes their individual invoices, so the
    /// consolidated one is once again the single ACTIVE invoice for those bills.
    ///
    /// <para>Refuses if any bill has since been consolidated onto a DIFFERENT invoice — reopening must never
    /// put a bill on two live invoices. That is the same double-invoice invariant, enforced on the way back.</para>
    /// </summary>
    public async Task ReopenAsync(long invoiceId, string? actor = null)
    {
        var invoice = await _db.CustomerInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new InvalidOperationException("Customer invoice not found.");

        if (invoice.Status != CustomerInvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Only a cancelled invoice can be reopened (this one is {invoice.Status}).");

        var user = actor ?? await CurrentUserAsync();

        // The bills this invoice originally covered — identified by the group key + being free again.
        var candidates = await _db.Bills
            .Where(b => b.CustomerInvoiceNumber != null
                     && b.CustomerInvoiceNumber.ToLower() == invoice.CustomerInvoiceNumber.ToLower()
                     && (b.Status == BillStatus.Approved || b.Status == BillStatus.Closed))
            .ToListAsync();

        var taken = candidates.Where(b => b.CustomerInvoiceId != null && b.CustomerInvoiceId != invoiceId).ToList();
        if (taken.Count > 0)
            throw new InvalidOperationException(
                "Cannot reopen: these bills are now on another customer invoice — "
                + string.Join(", ", taken.Select(b => b.BillNo)) + ".");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var b in candidates)
        {
            b.CustomerInvoiceId = invoiceId;
            if (b.InvoiceStatus == InvoiceStatus.Issued)
                b.InvoiceStatus = InvoiceStatus.Superseded;   // the consolidated invoice is active again
            b.ModifiedOn = DateTime.UtcNow;
            b.ModifiedBy = user;
        }

        invoice.Status             = CustomerInvoiceStatus.Issued;
        invoice.CancelledOn        = null;
        invoice.CancelledBy        = null;
        invoice.CancellationReason = null;

        LogEvent(invoice, "Invoice Reopened", $"{candidates.Count} bill(s) re-attached.", user);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogInformation("Customer invoice {InvoiceNo} reopened by {User}.", invoice.InvoiceNo, user);
    }

    /// <summary>Appends a timeline entry. Kind = Activity so it reads as a user-facing event (the Audit rows
    /// written by DuplicateBillingService stay distinct).</summary>
    private void LogEvent(CustomerInvoice invoice, string summary, string? detail, string actor)
    {
        _db.WorkflowAuditLogs.Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Activity,
            Module     = "Billing",
            EntityType = "CustomerInvoice",
            EntityId   = invoice.Id,
            EntityRef  = invoice.InvoiceNo,
            Operation  = WorkflowOperationType.Update,
            Summary    = summary,
            Details    = detail,
            Actor      = actor,
            At         = DateTime.UtcNow,
        });
    }

    // ── Numbering ────────────────────────────────────────────────────────────

    /// <summary>
    /// Next consolidated-invoice number: "CI/26-27/0001". A sequence of its own, deliberately independent of
    /// <see cref="Bill.BillNo"/> and of the per-bill <see cref="Bill.InvoiceNumber"/> (which stays derived 1:1
    /// from the bill number and is NOT touched). Same per-FY rule as
    /// <see cref="BillService.NextBillNoAsync"/> — the numbering convention is copied, not the code path,
    /// because the two sequences must never share a counter.
    /// </summary>
    public static async Task<string> NextInvoiceNoAsync(AppDbContext db, int finYear)
    {
        var fyDisplay = FinancialYear.Display(finYear);

        var lastNo = await db.CustomerInvoices
            .Where(i => i.FinYear == finYear)
            .OrderByDescending(i => i.Id)
            .Select(i => i.InvoiceNo)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (!string.IsNullOrEmpty(lastNo))
        {
            var tail = lastNo.Split('/').Last();
            if (int.TryParse(tail, out var n)) seq = n + 1;
        }
        return $"CI/{fyDisplay}/{seq:D4}";
    }

    private static DateTime? ComputeDueDate(DateTime issueDate, string? paymentTerms)
    {
        if (string.IsNullOrWhiteSpace(paymentTerms)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(paymentTerms, @"\d+");
        return m.Success ? issueDate.AddDays(int.Parse(m.Value)) : (DateTime?)null;
    }
}
