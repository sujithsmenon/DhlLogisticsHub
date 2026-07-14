namespace DhlLogistics.Web.Service.Search.Providers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>Shared bill query + projection so the customer-bill and transportation-bill providers hold ZERO
/// duplicated query logic — they differ only by the mode filter, group label and quick actions.</summary>
internal static class BillSearch
{
    internal sealed record Row(string BillNo, string? InvoiceNumber, string? CustomerInvoiceNumber,
                               BillMode Mode, BillStatus Status, DateTime BillDate, string? Client, string? Branch);

    internal static async Task<List<Row>> FetchAsync(IQueryable<Bill> scope, SearchQuery q, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        if (q.HasText)
            scope = scope.Where(b =>
                   EF.Functions.ILike(b.BillNo, like)
                || EF.Functions.ILike(b.BillNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || (b.InvoiceNumber != null && (EF.Functions.ILike(b.InvoiceNumber, like)
                        || EF.Functions.ILike(b.InvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || (b.CustomerInvoiceNumber != null && EF.Functions.ILike(b.CustomerInvoiceNumber, like))
                || (b.Reference != null && EF.Functions.ILike(b.Reference, like))
                || (b.Remarks != null && EF.Functions.ILike(b.Remarks, like))
                || (b.AwbOrBlNumber != null && EF.Functions.ILike(b.AwbOrBlNumber, like))
                || (b.ContainerNumber != null && EF.Functions.ILike(b.ContainerNumber, like))
                || (b.VehicleNumber != null && EF.Functions.ILike(b.VehicleNumber, like))
                || (b.DriverName != null && EF.Functions.ILike(b.DriverName, like))
                || (b.SourceReference != null && EF.Functions.ILike(b.SourceReference, like))
                || EF.Functions.ILike(b.BillingClient!.CompanyName, like));

        return await scope.OrderByDescending(b => b.Id).Take(SearchProviderBase.FetchN)
            .Select(b => new Row(b.BillNo, b.InvoiceNumber, b.CustomerInvoiceNumber, b.Mode, b.Status, b.BillDate,
                                 b.BillingClient!.CompanyName, b.Branch!.BranchName))
            .ToListAsync(ct);
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
                new QuickAction("Verify", "✔️", "/bills/verify"),
                new QuickAction("Approve / Post", "✅", "/bills/approve"),
            });
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
                new QuickAction("Verify", "✔️", "/bills/verify"),
                new QuickAction("Post to Accounts", "✅", "/bills/approve"),
            }));
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
            })
            .ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(
            Module, Icon, r.InvoiceNo,
            $"{r.Client} · Cust Inv {r.CustomerInvoiceNumber} · {r.Bills} bill(s)",
            r.Status.ToString(), r.Branch, r.InvoiceDate,
            "/bills/customer-invoices", new[]
            {
                new QuickAction("View", "📂", "/bills/customer-invoices"),
            }));
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
