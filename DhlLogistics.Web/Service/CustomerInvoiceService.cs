namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

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

            _log.LogInformation(
                "Customer invoice {InvoiceNo} SUPERSEDES {Count} individual invoice(s): {Bills}.",
                invoice.InvoiceNo, toSupersede.Count,
                string.Join(", ", toSupersede.Select(b => $"{b.BillNo}/{b.InvoiceNumber}")));
        }

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

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogInformation("Customer invoice {InvoiceNo} cancelled by {User}.", invoice.InvoiceNo, user);
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
        var fyDisplay = $"{(finYear % 100):D2}-{((finYear + 1) % 100):D2}";

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
