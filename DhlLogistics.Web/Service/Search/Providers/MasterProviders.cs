namespace DhlLogistics.Web.Service.Search.Providers;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

// ── Clients ───────────────────────────────────────────────────────────────────
public sealed class ClientSearchProvider : SearchProviderBase
{
    public override string   Module          => "Clients";
    public override string   Icon            => "🏢";
    public override string[] Keywords        => new[] { "client", "clients", "customer", "customers" };
    public override string[] PermissionPaths => new[] { "masters/clients" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.Clients.AsNoTracking();
        if (q.HasText)
            query = query.Where(c => EF.Functions.ILike(c.CompanyName, like)
                                  || EF.Functions.ILike(c.ContactEmail, like)
                                  || EF.Functions.ILike(c.Phone, like)
                                  || EF.Functions.ILike(c.Address, like));

        var rows = await query.OrderBy(c => c.CompanyName).Take(Fetch)
            .Select(c => new { c.CompanyName, c.ContactEmail, c.Phone }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.CompanyName,
            string.IsNullOrWhiteSpace(r.ContactEmail) ? r.Phone : r.ContactEmail, null, null, null,
            "/masters/clients", new[]
            {
                new QuickAction("Open Profile", "📂", "/masters/clients"),
                new QuickAction("Create Job", "📦", "/jobs/clearance"),
                new QuickAction("Create Invoice", "💰", "/bills/clearance"),
                new QuickAction("Create Payment", "💵", "/finance/payments"),
            }));
        return Rank(hits, q, take);
    }
}

// ── Transporters ──────────────────────────────────────────────────────────────
public sealed class TransporterSearchProvider : SearchProviderBase
{
    public override string   Module          => "Transporters";
    public override string   Icon            => "🚛";
    public override string[] Keywords        => new[] { "transporter", "transporters" };
    public override string[] PermissionPaths => new[] { "masters/transporters" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.Transporters.AsNoTracking();
        if (q.HasText)
            query = query.Where(t => EF.Functions.ILike(t.CompanyName, like)
                                  || EF.Functions.ILike(t.ContactPerson, like)
                                  || EF.Functions.ILike(t.Phone, like)
                                  || EF.Functions.ILike(t.WhatsAppNumber, like)
                                  || EF.Functions.ILike(t.Email, like));

        var rows = await query.OrderBy(t => t.CompanyName).Take(Fetch)
            .Select(t => new { t.CompanyName, t.Phone, t.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.CompanyName, r.Phone,
            r.IsActive ? "Active" : "Inactive", null, null, "/masters/transporters",
            new[] { new QuickAction("View", "📂", "/masters/transporters") }));
        return Rank(hits, q, take);
    }
}

// ── Vehicles / Fleet ──────────────────────────────────────────────────────────
public sealed class VehicleSearchProvider : SearchProviderBase
{
    public override string   Module          => "Vehicles";
    public override string   Icon            => "🚛";
    public override string[] Keywords        => new[] { "vehicle", "vehicles", "fleet", "truck" };
    public override string[] PermissionPaths => new[] { "masters/vehicles" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.Vehicles.AsNoTracking();
        if (q.HasText)
            query = query.Where(v => EF.Functions.ILike(v.PlateNumber, like)
                                  || EF.Functions.ILike(v.PlateNumber.Replace("-", "").Replace(" ", ""), norm)
                                  || EF.Functions.ILike(v.VehicleType, like));

        var rows = await query.OrderBy(v => v.PlateNumber).Take(Fetch)
            .Select(v => new { v.PlateNumber, v.VehicleType, v.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.PlateNumber, r.VehicleType,
            r.IsActive ? "Active" : "Inactive", null, null, "/masters/vehicles", new[]
            {
                new QuickAction("View", "📂", "/masters/vehicles"),
                new QuickAction("Assign Driver", "👤", "/masters/vehicle-drivers"),
            }));
        return Rank(hits, q, take);
    }
}

// ── Drivers ───────────────────────────────────────────────────────────────────
public sealed class DriverSearchProvider : SearchProviderBase
{
    public override string   Module          => "Drivers";
    public override string   Icon            => "👤";
    public override string[] Keywords        => new[] { "driver", "drivers" };
    public override string[] PermissionPaths => new[] { "masters/vehicle-drivers" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.VehicleDrivers.AsNoTracking();
        if (q.HasText)
            query = query.Where(d => EF.Functions.ILike(d.DriverName, like)
                                  || EF.Functions.ILike(d.Phone, like)
                                  || EF.Functions.ILike(d.LicenseNo, like));

        var rows = await query.OrderBy(d => d.DriverName).Take(Fetch)
            .Select(d => new { d.DriverName, d.Phone, d.LicenseNo, d.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.DriverName,
            string.IsNullOrWhiteSpace(r.Phone) ? r.LicenseNo : r.Phone, r.IsActive ? "Active" : "Inactive", null, null,
            "/masters/vehicle-drivers", new[]
            {
                new QuickAction("View", "📂", "/masters/vehicle-drivers"),
                new QuickAction("Assignments", "🚛", "/masters/vehicles"),
            }));
        return Rank(hits, q, take);
    }
}

// ── Staff / Users ─────────────────────────────────────────────────────────────
public sealed class StaffSearchProvider : SearchProviderBase
{
    public override string   Module          => "Staff";
    public override string   Icon            => "👤";
    public override string[] Keywords        => new[] { "staff", "user", "users", "employee" };
    public override string[] PermissionPaths => new[] { "masters/staff" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.Staff.AsNoTracking();
        if (q.HasText)
            query = query.Where(s => EF.Functions.ILike(s.FullName, like)
                                  || EF.Functions.ILike(s.Email, like)
                                  || EF.Functions.ILike(s.Phone, like));

        var rows = await query.OrderBy(s => s.FullName).Take(Fetch)
            .Select(s => new { s.FullName, s.Email, s.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.FullName, r.Email,
            r.IsActive ? "Active" : "Inactive", null, null, "/masters/staff",
            new[] { new QuickAction("View", "📂", "/masters/staff") }));
        return Rank(hits, q, take);
    }
}

// ── Commodities ───────────────────────────────────────────────────────────────
public sealed class CommoditySearchProvider : SearchProviderBase
{
    public override string   Module          => "Commodities";
    public override string   Icon            => "📦";
    public override string[] Keywords        => new[] { "commodity", "commodities", "hs", "hscode" };
    public override string[] PermissionPaths => new[] { "masters/commodities" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.Commodities.AsNoTracking();
        if (q.HasText)
            query = query.Where(c => EF.Functions.ILike(c.CommodityName, like)
                                  || EF.Functions.ILike(c.HsCode, like));

        var rows = await query.OrderBy(c => c.CommodityName).Take(Fetch)
            .Select(c => new { c.CommodityName, c.HsCode }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.CommodityName,
            string.IsNullOrWhiteSpace(r.HsCode) ? null : $"HS {r.HsCode}", null, null, null,
            "/masters/commodities", new[] { new QuickAction("View", "📂", "/masters/commodities") }));
        return Rank(hits, q, take);
    }
}

// ── Ports ─────────────────────────────────────────────────────────────────────
public sealed class PortSearchProvider : SearchProviderBase
{
    public override string   Module          => "Ports";
    public override string   Icon            => "⚓";
    public override string[] Keywords        => new[] { "port", "ports" };
    public override string[] PermissionPaths => new[] { "masters/ports" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.Ports.AsNoTracking();
        if (q.HasText)
            query = query.Where(p => EF.Functions.ILike(p.PortName, like)
                                  || EF.Functions.ILike(p.PortCode, like)
                                  || EF.Functions.ILike(p.City, like));

        var rows = await query.OrderBy(p => p.PortName).Take(Fetch)
            .Select(p => new { p.PortName, p.PortCode, p.City }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.PortName,
            $"{r.PortCode} · {r.City}", null, null, null, "/masters/ports",
            new[] { new QuickAction("View", "📂", "/masters/ports") }));
        return Rank(hits, q, take);
    }
}

// ── Branches ──────────────────────────────────────────────────────────────────
public sealed class BranchSearchProvider : SearchProviderBase
{
    public override string   Module          => "Branches";
    public override string   Icon            => "🏢";
    public override string[] Keywords        => new[] { "branch", "branches" };
    public override string[] PermissionPaths => new[] { "masters/branches" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.CompanyBranches.AsNoTracking();
        if (q.HasText)
            query = query.Where(b => EF.Functions.ILike(b.BranchName, like)
                                  || EF.Functions.ILike(b.BranchCode, like)
                                  || EF.Functions.ILike(b.City, like));

        var rows = await query.OrderBy(b => b.BranchName).Take(Fetch)
            .Select(b => new { b.BranchName, b.BranchCode, b.City, b.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.BranchName,
            $"{r.BranchCode} · {r.City}", r.IsActive ? "Active" : "Inactive", null, null,
            "/masters/branches", new[] { new QuickAction("View", "📂", "/masters/branches") }));
        return Rank(hits, q, take);
    }
}

// ── Containers ────────────────────────────────────────────────────────────────
public sealed class ContainerSearchProvider : SearchProviderBase
{
    public override string   Module          => "Containers";
    public override string   Icon            => "📦";
    public override string[] Keywords        => new[] { "container", "containers", "cont" };
    public override string[] PermissionPaths => new[] { "masters/containers" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like; var norm = q.NormalizedLike;
        var query = db.Containers.AsNoTracking();
        if (q.HasText)
            query = query.Where(c => EF.Functions.ILike(c.ContainerNumber, like)
                                  || EF.Functions.ILike(c.ContainerNumber.Replace("-", "").Replace(" ", ""), norm)
                                  || EF.Functions.ILike(c.ContainerType, like));

        var rows = await query.OrderByDescending(c => c.Id).Take(Fetch)
            .Select(c => new { c.ContainerNumber, c.ContainerType, c.Status }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.ContainerNumber, r.ContainerType,
            r.Status.ToString(), null, null, "/masters/containers", new[]
            {
                new QuickAction("View", "📂", "/masters/containers"),
                new QuickAction("Movement / Track", "📍", "/tracking"),
            }));
        return Rank(hits, q, take);
    }
}

// ── Account heads / Ledger ────────────────────────────────────────────────────
public sealed class AccountHeadSearchProvider : SearchProviderBase
{
    public override string   Module          => "Accounts";
    public override string   Icon            => "📒";
    public override string[] Keywords        => new[] { "account", "accounts", "ledger", "head" };
    public override string[] PermissionPaths => new[] { "accounts/heads" };

    public override async Task<List<SearchHit>> SearchAsync(AppDbContext db, SearchQuery q, int take, CancellationToken ct)
    {
        var like = q.Like;
        var query = db.AccountHeads.AsNoTracking();
        if (q.HasText)
            query = query.Where(a => EF.Functions.ILike(a.AccountName, like)
                                  || EF.Functions.ILike(a.AccountCode, like));

        var rows = await query.OrderBy(a => a.AccountName).Take(Fetch)
            .Select(a => new { a.AccountName, a.AccountCode, a.IsActive }).ToListAsync(ct);

        var hits = rows.Select(r => new SearchHit(Module, Icon, r.AccountName, r.AccountCode,
            r.IsActive ? "Active" : "Inactive", null, null, "/accounts/heads", new[]
            {
                new QuickAction("View", "📂", "/accounts/heads"),
                new QuickAction("Ledger", "📖", "/reports/ledger"),
            }));
        return Rank(hits, q, take);
    }
}
