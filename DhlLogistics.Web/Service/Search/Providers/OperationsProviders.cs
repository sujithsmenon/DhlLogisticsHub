namespace DhlLogistics.Web.Service.Search.Providers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

// ── Clearance / Forwarding / Import job orders ────────────────────────────────
public sealed class JobOrderSearchProvider : SearchProviderBase
{
    public override string   Module          => "Jobs";
    public override string   Icon            => "📦";
    public override string[] Keywords        => new[] { "job", "jobs", "clearance", "forwarding", "import" };
    public override string[] PermissionPaths => new[] { "jobs/clearance", "jobs/forwarding" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.JobOrders.AsNoTracking();
        if (q.HasText)
            query = query.Where(j =>
                   EF.Functions.ILike(j.JobOrderNo, like)
                || EF.Functions.ILike(j.JobOrderNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || (j.CustomerInvoiceNumber != null && (EF.Functions.ILike(j.CustomerInvoiceNumber, like)
                        || EF.Functions.ILike(j.CustomerInvoiceNumber.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)))
                || EF.Functions.ILike(j.BillingClient!.CompanyName, like)
                || EF.Functions.ILike(j.Branch!.BranchName, like)
                || (j.Shipper   != null && EF.Functions.ILike(j.Shipper.CompanyName, like))
                || (j.Consignee != null && EF.Functions.ILike(j.Consignee.CompanyName, like))
                || (j.Commodity != null && EF.Functions.ILike(j.Commodity.CommodityName, like))
                // Ports of loading / discharge.
                || (j.LoadPort      != null && EF.Functions.ILike(j.LoadPort.PortName, like))
                || (j.DischargePort != null && EF.Functions.ILike(j.DischargePort.PortName, like))
                || (j.CargoDescription != null && EF.Functions.ILike(j.CargoDescription, like))
                || (j.Remarks != null && EF.Functions.ILike(j.Remarks, like)));

        var rows = await query.OrderByDescending(j => j.Id).Take(Fetch)
            .Select(j => new { j.Id, j.Mode, j.JobOrderNo, j.CustomerInvoiceNumber, j.Status, j.JobOrderDate,
                               Client = j.BillingClient!.CompanyName, Branch = j.Branch!.BranchName,
                               LoadPort = j.LoadPort!.PortName, DischargePort = j.DischargePort!.PortName,
                               // The chain, gathered in the SAME query — no per-row follow-up. (JobOrder has no
                               // Bills navigation, so the bills are counted by the group key they share.)
                               BillNos = db.Bills.Where(b => b.JobOrderId == j.Id).Select(b => b.BillNo).ToList() })
            .ToListAsync(ct);

        var hits = rows.Select(r =>
        {
            var url = r.Mode == JobMode.Forwarding ? "/jobs/forwarding" : "/jobs/clearance";
            var billUrl = r.Mode == JobMode.Forwarding ? "/bills/forwarding" : "/bills/clearance";
            return new SearchHit(Module, Icon, r.JobOrderNo,
                string.IsNullOrWhiteSpace(r.CustomerInvoiceNumber) ? r.Client : $"{r.Client} · Cust Inv {r.CustomerInvoiceNumber}",
                r.Status.ToString(), r.Branch, r.JobOrderDate, url, new[]
                {
                    new QuickAction("View", "📂", url),
                    new QuickAction("Verify", "✔️", "/jobs/verify"),
                    new QuickAction("Approve", "✅", "/jobs/approve"),
                    new QuickAction("Generate Bill", "💰", billUrl),
                    new QuickAction("Transportation Bill", "🚚", "/bills/transportation"),
                })
                {
                    Related = RelatedLine(r.CustomerInvoiceNumber, r.BillNos, r.LoadPort, r.DischargePort),
                };
        });
        return Rank(hits, q, take);
    }

    /// <summary>The Billing Group chain shown under a job hit — built from the row already fetched.</summary>
    private static string? RelatedLine(string? cinv, List<string> billNos, string? loadPort, string? dischargePort)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(cinv))       parts.Add($"Group {cinv}");
        if (billNos.Count > 0)                      parts.Add($"Bills {string.Join(", ", billNos)}");
        if (!string.IsNullOrWhiteSpace(loadPort) || !string.IsNullOrWhiteSpace(dischargePort))
            parts.Add($"{loadPort ?? "—"} → {dischargePort ?? "—"}");
        return parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }
}

// ── AWB shipments ─────────────────────────────────────────────────────────────
public sealed class AwbSearchProvider : SearchProviderBase
{
    public override string   Module          => "AWB Shipments";
    public override string   Icon            => "✈️";
    public override string[] Keywords        => new[] { "awb", "mawb", "hawb", "shipment", "shipments" };
    public override string[] PermissionPaths => new[] { "awb" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.AwbShipments.AsNoTracking();
        if (q.HasText)
            query = query.Where(a =>
                   EF.Functions.ILike(a.HawbNo, like)
                || EF.Functions.ILike(a.HawbNo.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || EF.Functions.ILike(a.ShipperName, like)
                || EF.Functions.ILike(a.ConsigneeName, like)
                || EF.Functions.ILike(a.ReferenceNumbers, like)
                || EF.Functions.ILike(a.OriginStation, like)
                || EF.Functions.ILike(a.DestinationStation, like)
                || EF.Functions.ILike(a.GoodsDescription, like)
                || (a.VehicleNumber != null && EF.Functions.ILike(a.VehicleNumber, like))
                || (a.DriverName != null && EF.Functions.ILike(a.DriverName, like))
                || (a.InvoiceNumber != null && EF.Functions.ILike(a.InvoiceNumber, like))
                // Billing Group key — searching CINV-10025 must return this shipment alongside its bills.
                || (a.CustomerInvoiceNumber != null && EF.Functions.ILike(a.CustomerInvoiceNumber, like)));

        var rows = await query.OrderByDescending(a => a.Id).Take(Fetch)
            .Select(a => new { a.HawbNo, a.ConsigneeName, a.OriginStation, a.DestinationStation, a.Status, a.ReceivedAt })
            .ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.HawbNo,
            $"{r.ConsigneeName} · {r.OriginStation}→{r.DestinationStation}", r.Status.ToString(), null, r.ReceivedAt,
            "/awb", new[]
            {
                new QuickAction("View", "📂", "/awb"),
                new QuickAction("Generate Bill", "🚚", "/bills/transportation"),
            }));
        return Rank(hits, q, take);
    }
}

// ── Export jobs ───────────────────────────────────────────────────────────────
public sealed class ExportJobSearchProvider : SearchProviderBase
{
    public override string   Module          => "Export Jobs";
    public override string   Icon            => "🚢";
    public override string[] Keywords        => new[] { "export", "sb", "shippingbill", "booking" };
    public override string[] PermissionPaths => new[] { "export" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.ExportJobs.AsNoTracking();
        if (q.HasText)
            query = query.Where(e =>
                   EF.Functions.ILike(e.JobReference, like)
                || EF.Functions.ILike(e.JobReference.Replace("-", "").Replace("/", "").Replace(" ", ""), norm)
                || EF.Functions.ILike(e.CustomerName, like)
                || EF.Functions.ILike(e.CargoDescription, like)
                || (e.ContainerNumber != null && EF.Functions.ILike(e.ContainerNumber, like))
                || (e.ShippingBillNumber != null && EF.Functions.ILike(e.ShippingBillNumber, like))
                || (e.BookingReference != null && EF.Functions.ILike(e.BookingReference, like))
                || (e.VehicleNumber != null && EF.Functions.ILike(e.VehicleNumber, like))
                || (e.DriverName != null && EF.Functions.ILike(e.DriverName, like))
                || (e.VesselName != null && EF.Functions.ILike(e.VesselName, like))
                // Billing Group key — searching CINV-10025 must return this job alongside its bills.
                || (e.CustomerInvoiceNumber != null && EF.Functions.ILike(e.CustomerInvoiceNumber, like)));

        var rows = await query.OrderByDescending(e => e.Id).Take(Fetch)
            .Select(e => new { e.JobReference, e.CustomerName, e.ContainerNumber, e.Status, e.ReceivedAt })
            .ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.JobReference,
            string.IsNullOrWhiteSpace(r.ContainerNumber) ? r.CustomerName : $"{r.CustomerName} · {r.ContainerNumber}",
            r.Status.ToString(), null, r.ReceivedAt, "/export", new[]
            {
                new QuickAction("View", "📂", "/export"),
                new QuickAction("Generate Bill", "🚚", "/bills/transportation"),
            }));
        return Rank(hits, q, take);
    }
}
