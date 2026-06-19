namespace DhlLogistics.Web.Database;

using DhlLogistics.Web.Model;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Creates the local SQL Server menu schema (EnsureCreated) and seeds it with the
/// items that were previously hard-coded in NavMenu.razor. Idempotent: if any rows
/// already exist the seed is skipped, so editing menus in the DB is never clobbered.
///
/// MenuId is a SQL Server IDENTITY column, so we never assign it explicitly. Parents
/// are saved first to obtain their generated ids, then children are linked via
/// ParentId. PageName values intentionally match the page-paths in
/// <see cref="PermissionSeed"/> so the per-user permission filter in NavMenu lines up
/// with the M3 permission matrix.
/// </summary>
public static class MenuSeed
{
    public static async Task SeedAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Schema is created by the AppDbContext migration (Postgres). Seed only if empty.
        if (await db.Menus.AnyAsync()) return;   // already seeded / hand-edited

        int order = 0;
        var topLevel = new List<Menu>();                       // groups + standalone leaves, in display order
        var childrenByParent = new List<(Menu Parent, List<Menu> Children)>();

        // Top-level group header (no page). Children added via the returned list.
        List<Menu> Group(string name, string icon, bool open = false)
        {
            var parent = new Menu
            {
                MenuName    = name,
                Icon        = icon,
                ParentId    = null,
                PageName    = null,
                ShowOrder   = order += 10,
                DefaultOpen = open,
                Active      = true,
            };
            topLevel.Add(parent);
            var children = new List<Menu>();
            childrenByParent.Add((parent, children));
            return children;
        }

        // Child leaf under a group.
        void Child(List<Menu> group, string name, string icon, string page, bool matchAll = false)
        {
            group.Add(new Menu
            {
                MenuName  = name,
                Icon      = icon,
                PageName  = page,
                ShowOrder = order += 10,
                MatchAll  = matchAll,
                Active    = true,
            });
        }

        // Standalone top-level leaf (Dashboard, Staff).
        void Leaf(string name, string icon, string page, bool isDashboard = false,
                  bool matchAll = false, bool requiresPermission = true)
        {
            topLevel.Add(new Menu
            {
                MenuName           = name,
                Icon               = icon,
                ParentId           = null,
                PageName           = page,
                ShowOrder          = order += 10,
                IsDashboard        = isDashboard,
                MatchAll           = matchAll,
                RequiresPermission = requiresPermission,
                Active             = true,
            });
        }

        // ── Dashboard ────────────────────────────────────────────────────────
        Leaf("Dashboard", "🏠", "", isDashboard: true, matchAll: true, requiresPermission: false);

        // ── Masters ──────────────────────────────────────────────────────────
        var masters = Group("Masters", "🗂", open: true);
        Child(masters, "Branches",            "🏬", "masters/branches");
        Child(masters, "Shipment Activities", "⚙",  "masters/shipment-activities");
        Child(masters, "Clients",             "🏢", "masters/clients");
        Child(masters, "Transporters",        "🚚", "masters/transporters");
        Child(masters, "Containers",          "📦", "masters/containers");
        Child(masters, "User Management",     "👤", "usermanagement");
        Child(masters, "Container Sizes",     "📐", "masters/container-sizes");
        Child(masters, "Commodities",         "📦", "masters/commodities");
        Child(masters, "Vessels",             "🚢", "masters/vessels");
        Child(masters, "Countries",           "🌐", "masters/countries");
        Child(masters, "Regions",             "🧭", "masters/regions");
        Child(masters, "States",              "📍", "masters/states");
        Child(masters, "Ports",               "⚓", "masters/ports");
        Child(masters, "SEZ Locations",       "🏭", "masters/sez-locations");

        // ── Operations ───────────────────────────────────────────────────────
        var ops = Group("Operations", "⚙");
        Child(ops, "AWB Shipments", "✈", "awb");
        Child(ops, "Export Jobs",   "📤", "export");
        Child(ops, "Jobs",          "📋", "jobs");
        Child(ops, "Live Tracking", "🗺", "tracking");
        Child(ops, "Cargo",         "📦", "cargo");

        // ── Billing ──────────────────────────────────────────────────────────
        var billing = Group("Billing", "🧾");
        Child(billing, "Overview",             "🏠", "finance/billing", matchAll: true);
        Child(billing, "Clearance Bills",      "🛃", "bills/clearance");
        Child(billing, "Forwarding Bills",     "📦", "bills/forwarding");
        Child(billing, "Transportation Bills", "🚛", "bills/transportation");
        Child(billing, "Verification",         "✔",  "bills/verify");
        Child(billing, "Approval",             "✅", "bills/approve");

        // ── Accounts ─────────────────────────────────────────────────────────
        var accounts = Group("Accounts", "📒");
        Child(accounts, "Overview",            "🏠", "finance/accounts", matchAll: true);
        Child(accounts, "Chart of Accounts",   "📚", "accounts/heads");
        Child(accounts, "Journal Vouchers",    "📝", "accounts/journal");
        Child(accounts, "Cash & Bank",         "💵", "accounts/cashbank");
        Child(accounts, "Verification",        "✔",  "accounts/verify");
        Child(accounts, "Approval & Posting",  "✅", "accounts/approve");

        // ── Reports ──────────────────────────────────────────────────────────
        var reports = Group("Reports", "📊");
        Child(reports, "Overview",       "🏠", "reports/finance", matchAll: true);
        Child(reports, "Account Ledger", "📒", "reports/ledger");
        Child(reports, "Trial Balance",  "⚖",  "reports/trial-balance");
        Child(reports, "GST Output",     "🧾", "reports/gst-output");
        Child(reports, "Bill Register",  "📑", "reports/bill-register");

        // ── Finance Masters ──────────────────────────────────────────────────
        var finMasters = Group("Finance Masters", "💼");
        Child(finMasters, "Currencies",   "💱", "masters/currencies");
        Child(finMasters, "SAC Codes",    "🧾", "masters/sac");
        Child(finMasters, "Charge Codes", "💰", "masters/charge-codes");

        // ── Staff (single tabbed page) ───────────────────────────────────────
        Leaf("Staff", "👥", "staff", requiresPermission: false);

        // ── Fleet ────────────────────────────────────────────────────────────
        var fleet = Group("Fleet", "🚛");
        Child(fleet, "Vehicles",           "🚛", "masters/vehicles");
        Child(fleet, "Vehicle Drivers",    "🧑‍✈️", "masters/vehicle-drivers");
        Child(fleet, "Vehicle Doc Types",  "📑", "masters/vehicle-document-types");
        Child(fleet, "Vehicle Documents",  "📄", "masters/vehicle-documents");
        Child(fleet, "Driver Doc Types",   "📑", "masters/driver-document-types");

        // ── Clearing ─────────────────────────────────────────────────────────
        var clearing = Group("Clearing", "🛃");
        Child(clearing, "Clearance Jobs", "📋", "jobs/clearance");
        Child(clearing, "Verification",   "✔",  "jobs/verify");
        Child(clearing, "Approval",       "✅", "jobs/approve");

        // ── Forwarding ───────────────────────────────────────────────────────
        var forwarding = Group("Forwarding", "📦");
        Child(forwarding, "Forwarding Jobs", "📋", "jobs/forwarding");
        Child(forwarding, "Verification",    "✔",  "jobs/verify");
        Child(forwarding, "Approval",        "✅", "jobs/approve");

        // Phase 1 — insert top-level rows so IDENTITY assigns their MenuIds.
        db.Menus.AddRange(topLevel);
        await db.SaveChangesAsync();

        // Phase 2 — link children to their now-known parent ids and insert.
        var allChildren = new List<Menu>();
        foreach (var (parent, children) in childrenByParent)
        {
            foreach (var child in children)
                child.ParentId = parent.MenuId;
            allChildren.AddRange(children);
        }
        db.Menus.AddRange(allChildren);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent fixup for menu databases that were already seeded before the CBM
    /// user-management migration: repoints the old "Users" (/masters/users) entry to
    /// the new /usermanagement page and hides the standalone "Roles" (/admin/permissions)
    /// entry, which /usermanagement now covers. Safe to run on every startup.
    /// </summary>
    public static async Task FixupAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Menus.AnyAsync()) return;   // nothing seeded yet — SeedAsync handles it

        var changed = false;

        var usersMenu = await db.Menus.FirstOrDefaultAsync(m => m.PageName == "masters/users");
        if (usersMenu is not null)
        {
            usersMenu.PageName = "usermanagement";
            usersMenu.MenuName = "User Management";
            changed = true;
        }

        var rolesMenu = await db.Menus.FirstOrDefaultAsync(m => m.PageName == "admin/permissions");
        if (rolesMenu is not null)
        {
            rolesMenu.Active = false;   // superseded by the Roles tab in /usermanagement
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }
}
