namespace DhlLogistics.Web.Service.Search.Providers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>Shared bill query + projection so the customer-bill and transportation-bill providers hold ZERO
/// duplicated query logic — they differ only by the mode filter, group label and quick actions.</summary>
internal static class BillSearch
{
    internal sealed record Row(string BillNo, string? InvoiceNumber, string? CustomerInvoiceNumber,
                               BillMode Mode, BillStatus Status, DateTime BillDate, string? Client, string? Branch,
                               string? Container, string? Vehicle, string? AwbOrBl, string? Origin, string? Destination,
                               string? JobNo, string? SystemInvoiceNo, int SiblingBills);

    internal static async Task<List<Row>> FetchAsync(IQueryable<Bill> scope, SearchQuery q, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;

        // Every code-shaped field is matched BOTH literally and separator-stripped, so "INV234354",
        // "INV-234354" and "INV/234354" all hit the same row.
        if (q.HasText)
            scope = scope.Where(b =>
                   EF.Functions.ILike(b.BillNo, like)
                || EF.Functions.ILike(b.BillNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || (b.InvoiceNumber != null && (EF.Functions.ILike(b.InvoiceNumber, like)
                        || EF.Functions.ILike(b.InvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.CustomerInvoiceNumber != null && (EF.Functions.ILike(b.CustomerInvoiceNumber, like)
                        || EF.Functions.ILike(b.CustomerInvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.Reference != null && EF.Functions.ILike(b.Reference, like))
                || (b.Remarks != null && EF.Functions.ILike(b.Remarks, like))
                || (b.AwbOrBlNumber != null && (EF.Functions.ILike(b.AwbOrBlNumber, like)
                        || EF.Functions.ILike(b.AwbOrBlNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.ContainerNumber != null && (EF.Functions.ILike(b.ContainerNumber, like)
                        || EF.Functions.ILike(b.ContainerNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.VehicleNumber != null && (EF.Functions.ILike(b.VehicleNumber, like)
                        || EF.Functions.ILike(b.VehicleNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.DriverName != null && EF.Functions.ILike(b.DriverName, like))
                || (b.SourceReference != null && EF.Functions.ILike(b.SourceReference, like))
                || (b.Origin != null && EF.Functions.ILike(b.Origin, like))
                || (b.Destination != null && EF.Functions.ILike(b.Destination, like))
                || (b.CommodityName != null && EF.Functions.ILike(b.CommodityName, like))
                || (b.ShipmentTypeName != null && EF.Functions.ILike(b.ShipmentTypeName, like))
                || EF.Functions.ILike(b.BillingClient!.CompanyName, like)
                || EF.Functions.ILike(b.Branch!.BranchName, like)
                // The consolidated invoice this bill sits on (system CI number).
                || (b.CustomerInvoice != null && (EF.Functions.ILike(b.CustomerInvoice.InvoiceNo, like)
                        || EF.Functions.ILike(b.CustomerInvoice.InvoiceNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                // The originating job.
                || (b.JobOrder != null && (EF.Functions.ILike(b.JobOrder.JobOrderNo, like)
                        || EF.Functions.ILike(b.JobOrder.JobOrderNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                // Charge-level: charge name / description / operation / category.
                || b.Charges.Any(c => EF.Functions.ILike(c.Description, like)
                                   || (c.OperationName != null && EF.Functions.ILike(c.OperationName, like))
                                   || (c.ChargeCode != null && EF.Functions.ILike(c.ChargeCode.ChargeName, like))));

        return await scope.OrderByDescending(b => b.Id).Take(SearchProviderBase.FetchN)
            .Select(b => new Row(b.BillNo, b.InvoiceNumber, b.CustomerInvoiceNumber, b.Mode, b.Status, b.BillDate,
                                 b.BillingClient!.CompanyName, b.Branch!.BranchName,
                                 b.ContainerNumber, b.VehicleNumber, b.AwbOrBlNumber, b.Origin, b.Destination,
                                 b.JobOrder!.JobOrderNo,
                                 b.CustomerInvoice!.InvoiceNo,
                                 // Related-records count, computed in SQL — no per-row follow-up query.
                                 b.CustomerInvoiceNumber == null ? 0
                                     : b.BillingClient!.Id == 0 ? 0
                                     : b.CustomerInvoice!.Bills.Count))
            .ToListAsync(ct);
    }

    /// <summary>The Billing Group chain shown under a bill hit — built from the row we already fetched.</summary>
    internal static string? RelatedLine(Row r)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.CustomerInvoiceNumber)) parts.Add($"Group {r.CustomerInvoiceNumber}");
        if (!string.IsNullOrWhiteSpace(r.SystemInvoiceNo))       parts.Add($"Invoice {r.SystemInvoiceNo}");
        if (!string.IsNullOrWhiteSpace(r.JobNo))                 parts.Add($"Job {r.JobNo}");
        if (!string.IsNullOrWhiteSpace(r.Container))             parts.Add($"Container {r.Container}");
        if (!string.IsNullOrWhiteSpace(r.Vehicle))               parts.Add($"Vehicle {r.Vehicle}");
        if (!string.IsNullOrWhiteSpace(r.AwbOrBl))               parts.Add($"AWB/BL {r.AwbOrBl}");
        return parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }

    internal static string RouteFor(BillMode m) => m switch
    {
        BillMode.Forwarding     => "/bills/forwarding",
        BillMode.Transportation => "/bills/transportation",
        _                       => "/bills/clearance",
    };
}

// ── Customer bills / invoices (Clearance + Forwarding) ────────────────────────
public sealed class BillSearchProvider : SearchProviderBase
{
    public override string   Module          => "Invoices / Bills";
    public override string   Icon            => "💰";
    public override string[] Keywords        => new[] { "invoice", "invoices", "bill", "bills", "inv" };
    public override string[] PermissionPaths => new[] { "bills/clearance", "bills/forwarding" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var scope = db.Bills.AsNoTracking().Where(b => b.Mode != BillMode.Transportation);
        var rows  = await BillSearch.FetchAsync(scope, q, ct);
        var hits  = rows.Select(r =>
        {
            var url = BillSearch.RouteFor(r.Mode);
            var primary = string.IsNullOrWhiteSpace(r.InvoiceNumber) ? r.BillNo : r.InvoiceNumber!;
            var sub = r.Client + (string.IsNullOrWhiteSpace(r.CustomerInvoiceNumber) ? "" : $" · Cust Inv {r.CustomerInvoiceNumber}");
            return new SearchHit(Module, Icon, primary, sub, r.Status.ToString(), r.Branch, r.BillDate, url, new[]
            {
                new QuickAction("View", "📂", url),
                new QuickAction("Customer Invoice", "🧮", "/bills/customer-invoices"),
                new QuickAction("Approve / Post", "✅", "/bills/approve"),
            })
            {
                Related  = BillSearch.RelatedLine(r),
                Customer = r.Client,
                Type     = r.Mode.ToString(),      // Bill Type filter
            };
        });
        return Rank(hits, q, take);
    }
}

// ── Transportation bills ──────────────────────────────────────────────────────
public sealed class TransportationBillSearchProvider : SearchProviderBase
{
    public override string   Module          => "Transportation Bills";
    public override string   Icon            => "🚚";
    public override string[] Keywords        => new[] { "transport", "transportation", "tb", "freight" };
    public override string[] PermissionPaths => new[] { "bills/transportation" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var scope = db.Bills.AsNoTracking().Where(b => b.Mode == BillMode.Transportation);
        var rows  = await BillSearch.FetchAsync(scope, q, ct);
        var hits  = rows.Select(r => new SearchHit(Module, Icon, r.BillNo, r.Client, r.Status.ToString(), r.Branch, r.BillDate,
            "/bills/transportation", new[]
            {
                new QuickAction("View", "📂", "/bills/transportation"),
                new QuickAction("Customer Invoice", "🧮", "/bills/customer-invoices"),
                new QuickAction("Post to Accounts", "✅", "/bills/approve"),
            })
            {
                Related  = BillSearch.RelatedLine(r),
                Customer = r.Client,
                Type     = r.Mode.ToString(),      // always "Transportation" here
            });
        return Rank(hits, q, take);
    }
}

// ── Consolidated customer invoices (Billing Group) ────────────────────────────
// Bills and Jobs already match on CustomerInvoiceNumber (see BillSearch above and the job provider), so
// searching a customer reference such as CINV-10025 ALREADY returns the group's jobs + bills. This provider
// adds the fourth member — the consolidated invoice itself — so one search surfaces the whole Billing Group.
public sealed class CustomerInvoiceSearchProvider : SearchProviderBase
{
    public override string   Module          => "Customer Invoices";
    public override string   Icon            => "🧮";
    public override string[] Keywords        => new[] { "customer invoice", "customerinvoice", "ci", "consolidated", "cinv" };
    public override string[] PermissionPaths => new[] { "bills/clearance", "bills/forwarding" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var scope = db.CustomerInvoices.AsNoTracking().AsQueryable();

        if (q.HasText)
            scope = scope.Where(i =>
                   EF.Functions.ILike(i.InvoiceNo, like)
                || EF.Functions.ILike(i.InvoiceNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                // the Billing Group key — searching CINV-10025 must find this invoice
                || EF.Functions.ILike(i.CustomerInvoiceNumber, like)
                || EF.Functions.ILike(i.CustomerInvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || (i.Remarks != null && EF.Functions.ILike(i.Remarks, like))
                || EF.Functions.ILike(i.BillingClient!.CompanyName, like)
                // a bill number in the group must also surface the invoice it was consolidated onto
                || i.Bills.Any(b => EF.Functions.ILike(b.BillNo, like)));

        var rows = await scope.OrderByDescending(i => i.Id).Take(SearchProviderBase.FetchN)
            .Select(i => new
            {
                i.Id, i.InvoiceNo, i.CustomerInvoiceNumber, i.InvoiceDate, i.Status, i.TotalAmount,
                Client = i.BillingClient!.CompanyName,
                Branch = i.Branch!.BranchName,
                Bills  = i.Bills.Count,
                // The chain, collected in the SAME query — no per-row follow-up.
                BillNos = i.Bills.Select(b => b.BillNo).ToList(),
                JobNos  = i.Bills.Where(b => b.JobOrder != null).Select(b => b.JobOrder!.JobOrderNo).Distinct().ToList(),
                Docs    = i.Documents.Count(d => d.IsActive),
            })
            .ToListAsync(ct);

        var hits = rows.Select(r =>
        {
            var chain = new List<string> { $"Group {r.CustomerInvoiceNumber}" };
            if (r.BillNos.Count > 0) chain.Add($"Bills {string.Join(", ", r.BillNos)}");
            if (r.JobNos.Count  > 0) chain.Add($"Jobs {string.Join(", ", r.JobNos)}");
            if (r.Docs > 0)          chain.Add($"{r.Docs} document(s)");

            return new SearchHit(
                Module, Icon, r.InvoiceNo,
                $"{r.Client} · Cust Inv {r.CustomerInvoiceNumber} · {r.Bills} bill(s) · {r.TotalAmount:N2}",
                r.Status.ToString(), r.Branch, r.InvoiceDate,
                "/bills/customer-invoices", new[]
                {
                    new QuickAction("View", "📂", "/bills/customer-invoices"),
                    new QuickAction("Bills", "💰", "/bills/clearance"),
                })
                {
                    Related  = string.Join("  ·  ", chain),
                    Customer = r.Client,
                };
        });
        return Rank(hits, q, take);
    }
}

// ── Documents (invoice PDFs — generated + uploaded) ───────────────────────────
// Makes the DOCUMENT itself findable, not just the record it hangs off: searching a bill number, an invoice
// number or part of a file name surfaces the PDF, including superseded versions (flagged, never hidden).
public sealed class DocumentSearchProvider : SearchProviderBase
{
    public override string   Module          => "Documents";
    public override string   Icon            => "📄";
    public override string[] Keywords        => new[] { "document", "documents", "pdf", "file", "attachment" };
    public override string[] PermissionPaths => new[] { "bills/clearance", "bills/forwarding", "bills/transportation" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var scope = db.InvoiceDocuments.AsNoTracking().AsQueryable();

        if (q.HasText)
            scope = scope.Where(d =>
                   EF.Functions.ILike(d.OriginalFileName, like)
                || EF.Functions.ILike(d.OriginalFileName.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                // findable by the bill it belongs to …
                || EF.Functions.ILike(d.Bill!.BillNo, like)
                || EF.Functions.ILike(d.Bill!.BillNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                // … by the Billing Group key (normalised too, so INV234354 == INV-234354 == INV/234354) …
                || (d.Bill!.CustomerInvoiceNumber != null && (EF.Functions.ILike(d.Bill!.CustomerInvoiceNumber, like)
                        || EF.Functions.ILike(d.Bill!.CustomerInvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                // … or by the consolidated invoice it is the PDF of
                || (d.CustomerInvoice != null && (EF.Functions.ILike(d.CustomerInvoice.InvoiceNo, like)
                        || EF.Functions.ILike(d.CustomerInvoice.InvoiceNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                        || EF.Functions.ILike(d.CustomerInvoice.CustomerInvoiceNumber, like)
                        || EF.Functions.ILike(d.CustomerInvoice.CustomerInvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm))));

        var rows = await scope.OrderByDescending(d => d.Id).Take(SearchProviderBase.FetchN)
            .Select(d => new
            {
                d.Id, d.OriginalFileName, d.DocumentType, d.Version, d.IsActive, d.UploadedDate, d.UploadedBy,
                BillNo = d.Bill!.BillNo,
                Cinv   = d.Bill!.CustomerInvoiceNumber,
                SysNo  = d.CustomerInvoice!.InvoiceNo,
                Client = d.Bill!.BillingClient!.CompanyName,
                Branch = d.Bill!.Branch!.BranchName,
            })
            .ToListAsync(ct);

        var hits = rows.Select(r =>
        {
            var chain = new List<string>();
            if (!string.IsNullOrWhiteSpace(r.SysNo)) chain.Add($"Invoice {r.SysNo}");
            if (!string.IsNullOrWhiteSpace(r.BillNo)) chain.Add($"Bill {r.BillNo}");
            if (!string.IsNullOrWhiteSpace(r.Cinv))   chain.Add($"Group {r.Cinv}");

            return new SearchHit(
                Module, Icon, r.OriginalFileName,
                $"{r.Client} · {r.DocumentType} · v{r.Version}",
                r.IsActive ? "Active" : "Superseded",
                r.Branch, r.UploadedDate,
                $"/invoices/doc/{r.Id}", new[]
                {
                    new QuickAction("Open", "📂", $"/invoices/doc/{r.Id}"),
                    new QuickAction("Download", "⬇️", $"/invoices/doc/{r.Id}?dl=true"),
                })
                { Related = chain.Count == 0 ? null : string.Join("  ·  ", chain) };
        });
        return Rank(hits, q, take);
    }
}

// ── Vouchers / Payments / Receipts / Journal ──────────────────────────────────
public sealed class VoucherSearchProvider : SearchProviderBase
{
    public override string   Module          => "Vouchers";
    public override string   Icon            => "🧾";
    public override string[] Keywords        => new[] { "voucher", "vouchers", "payment", "receipt", "journal", "cheque" };
    public override string[] PermissionPaths => new[] { "accounts/journal" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.Vouchers.AsNoTracking();
        if (q.HasText)
            query = query.Where(v =>
                   EF.Functions.ILike(v.VoucherNo, like)
                || EF.Functions.ILike(v.VoucherNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || (v.ReferenceNo != null && EF.Functions.ILike(v.ReferenceNo, like))
                || EF.Functions.ILike(v.Narration, like));

        var rows = await query.OrderByDescending(v => v.Id).Take(Fetch)
            .Select(v => new { v.VoucherNo, v.Type, v.ReferenceNo, v.Status, v.VoucherDate, v.TotalDebit,
                               Branch = v.Branch!.BranchName })
            .ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.VoucherNo,
            $"{r.Type} · {r.TotalDebit:N2}" + (string.IsNullOrWhiteSpace(r.ReferenceNo) ? "" : $" · Ref {r.ReferenceNo}"),
            r.Status.ToString(), r.Branch, r.VoucherDate, "/accounts/journal", new[]
            {
                new QuickAction("View", "📂", "/accounts/journal"),
                new QuickAction("Approve / Post", "✅", "/accounts/approve"),
            }));
        return Rank(hits, q, take);
    }
}
