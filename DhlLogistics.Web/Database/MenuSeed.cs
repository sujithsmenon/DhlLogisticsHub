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
        void Child(List<Menu> group, string name, string icon, string page, bool matchAll = false, bool requiresPermission = true)
        {
            group.Add(new Menu
            {
                MenuName           = name,
                Icon               = icon,
                PageName           = page,
                ShowOrder          = order += 10,
                MatchAll           = matchAll,
                RequiresPermission = requiresPermission,
                Active             = true,
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
        Child(masters, "Company Details",     "🏢", "masters/company-details");

        // ── Operations ───────────────────────────────────────────────────────
        var ops = Group("Operations", "⚙");
        Child(ops, "Operations Dashboard", "📊", "operations/dashboard", requiresPermission: false);
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
        Child(billing, "Customer Invoices",    "🧮", "bills/customer-invoices");
        Child(billing, "Verification",         "✔",  "bills/verify");
        Child(billing, "Approval",             "✅", "bills/approve");

        // ── Accounts ─────────────────────────────────────────────────────────
        var accounts = Group("Accounts", "📒");
        Child(accounts, "Overview",            "🏠", "finance/accounts", matchAll: true);
        Child(accounts, "Chart of Accounts",   "📚", "accounts/heads");
        Child(accounts, "Journal Vouchers",    "📝", "accounts/journal");
        Child(accounts, "Payments",            "💰", "finance/payments");
        Child(accounts, "Cash & Bank",         "💵", "accounts/cashbank");
        Child(accounts, "Verification",        "✔",  "accounts/verify");
        Child(accounts, "Approval & Posting",  "✅", "accounts/approve");

        // ── Reports ──────────────────────────────────────────────────────────
        var reports = Group("Reports", "📊");
        Child(reports, "Overview",          "🏠", "reports/finance", matchAll: true);
        Child(reports, "Account Ledger",    "📒", "reports/ledger");
        Child(reports, "Trial Balance",     "⚖",  "reports/trial-balance");
        Child(reports, "Profit & Loss",     "📈", "reports/profit-loss");
        Child(reports, "Balance Sheet",     "🧮", "reports/balance-sheet");
        Child(reports, "Revenue",           "💹", "reports/revenue");
        Child(reports, "Expense",           "💸", "reports/expense");
        Child(reports, "Job Profitability", "📊", "reports/job-profitability");
        Child(reports, "Receivable Aging",  "⏳", "reports/receivable-aging");
        Child(reports, "Payable Aging",     "⌛", "reports/payable-aging");
        Child(reports, "Payment Register",  "🧾", "reports/payment-register");
        Child(reports, "Customer Statement","👤", "reports/customer-statement");
        Child(reports, "Vendor Statement",  "🏭", "reports/vendor-statement");
        Child(reports, "Cash Book",         "💵", "reports/cash-book");
        Child(reports, "Bank Book",         "🏦", "reports/bank-book");
        Child(reports, "Forwarding Report", "📦", "reports/forwarding");
        Child(reports, "Clearance Report",  "🛃", "reports/clearance");
        Child(reports, "Transportation Report","🚛", "reports/transportation");
        Child(reports, "KPI Dashboard",     "📊", "reports/kpi");
        Child(reports, "GST Output",        "🧾", "reports/gst-output");
        Child(reports, "Bill Register",     "📑", "reports/bill-register");

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
    /// Idempotent additive seed for the ERP finance reports + payments pages. Because
    /// <see cref="SeedAsync"/> only runs on an empty table, already-seeded installs would never
    /// gain the new report pages — so this inserts any missing leaf (keyed by PageName) under the
    /// existing <b>Reports</b> and <b>Accounts</b> groups. Safe to run on every startup: existing rows
    /// are left untouched, so hand-edited menus are never clobbered. No menu item is hard-coded in the
    /// UI — the sidebar still renders purely from these DB rows, permission-filtered per user.
    /// </summary>
    public static async Task EnsureFinanceMenusAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Menus.AnyAsync()) return;   // fresh DB — SeedAsync will cover it

        var reports  = await db.Menus.FirstOrDefaultAsync(m => m.ParentId == null && m.MenuName == "Reports");
        var accounts = await db.Menus.FirstOrDefaultAsync(m => m.ParentId == null && m.MenuName == "Accounts");
        if (reports is null && accounts is null) return;

        var existingPages = new HashSet<string>(
            await db.Menus.Where(m => m.PageName != null).Select(m => m.PageName!).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var nextOrder = (await db.Menus.MaxAsync(m => (int?)m.ShowOrder) ?? 0) + 10;
        var toAdd = new List<Menu>();

        void Add(Menu? parent, string name, string icon, string page)
        {
            if (parent is null || existingPages.Contains(page)) return;
            toAdd.Add(new Menu
            {
                MenuName           = name,
                Icon               = icon,
                PageName           = page,
                ParentId           = parent.MenuId,
                ShowOrder          = nextOrder,
                RequiresPermission = true,
                Active             = true,
            });
            existingPages.Add(page);
            nextOrder += 10;
        }

        // New finance report pages under the existing Reports group.
        Add(reports, "Profit & Loss",       "📈", "reports/profit-loss");
        Add(reports, "Balance Sheet",       "🧮", "reports/balance-sheet");
        Add(reports, "Revenue",             "💹", "reports/revenue");
        Add(reports, "Expense",             "💸", "reports/expense");
        Add(reports, "Job Profitability",   "📊", "reports/job-profitability");
        Add(reports, "Receivable Aging",    "⏳", "reports/receivable-aging");
        Add(reports, "Payable Aging",       "⌛", "reports/payable-aging");
        Add(reports, "Payment Register",    "🧾", "reports/payment-register");
        Add(reports, "Customer Statement",  "👤", "reports/customer-statement");
        Add(reports, "Vendor Statement",    "🏭", "reports/vendor-statement");
        Add(reports, "Cash Book",           "💵", "reports/cash-book");
        Add(reports, "Bank Book",           "🏦", "reports/bank-book");
        Add(reports, "Forwarding Report",   "📦", "reports/forwarding");
        Add(reports, "Clearance Report",    "🛃", "reports/clearance");
        Add(reports, "Transportation Report","🚛", "reports/transportation");
        Add(reports, "KPI Dashboard",       "📊", "reports/kpi");

        // Payments entry under the existing Accounts group.
        Add(accounts, "Payments", "💰", "finance/payments");

        if (toAdd.Count == 0) return;
        db.Menus.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent additive seed for the Operations Dashboard leaf under the existing <b>Operations</b>
    /// group. Because <see cref="SeedAsync"/> only runs on an empty table, already-seeded installs would
    /// never gain the new page — this inserts it if missing (keyed by PageName). Placed first in the
    /// group and permission-free so it's visible to every authenticated user. Safe on every startup.
    /// </summary>
    public static async Task EnsureOperationsMenusAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Menus.AnyAsync()) return;   // fresh DB — SeedAsync will cover it

        const string page = "operations/dashboard";
        if (await db.Menus.AnyAsync(m => m.PageName == page)) return;   // already present

        var ops = await db.Menus.FirstOrDefaultAsync(m => m.ParentId == null && m.MenuName == "Operations");
        if (ops is null) return;

        // Sort just ahead of the group's current first child so it heads the list.
        var minChildOrder = await db.Menus.Where(m => m.ParentId == ops.MenuId)
            .MinAsync(m => (int?)m.ShowOrder) ?? (ops.ShowOrder + 1);

        db.Menus.Add(new Menu
        {
            MenuName           = "Operations Dashboard",
            Icon               = "📊",
            PageName           = page,
            ParentId           = ops.MenuId,
            ShowOrder          = minChildOrder - 1,
            RequiresPermission = false,
            Active             = true,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent additive seed for the Billing → Customer Invoices leaf. <see cref="SeedAsync"/> only runs on
    /// an empty table, so an already-seeded install would never gain the new page. Inserted after
    /// Transportation Bills, keyed by PageName. Safe on every startup.
    /// </summary>
    public static async Task EnsureCustomerInvoiceMenuAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Menus.AnyAsync()) return;   // fresh DB — SeedAsync covers it

        const string page = "bills/customer-invoices";
        if (await db.Menus.AnyAsync(m => m.PageName == page)) return;   // already present

        var billing = await db.Menus.FirstOrDefaultAsync(m => m.ParentId == null && m.MenuName == "Billing");
        if (billing is null) return;

        // Slot it just after Transportation Bills; fall back to the end of the group.
        var afterOrder = await db.Menus
            .Where(m => m.ParentId == billing.MenuId && m.PageName == "bills/transportation")
            .Select(m => (int?)m.ShowOrder).FirstOrDefaultAsync()
            ?? await db.Menus.Where(m => m.ParentId == billing.MenuId).MaxAsync(m => (int?)m.ShowOrder)
            ?? billing.ShowOrder;

        db.Menus.Add(new Menu
        {
            MenuName           = "Customer Invoices",
            Icon               = "🧮",
            PageName           = page,
            ParentId           = billing.MenuId,
            ShowOrder          = afterOrder + 1,
            RequiresPermission = false,
            Active             = true,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent additive seed for the Masters → Company Details leaf on already-seeded installs.
    /// Because <see cref="SeedAsync"/> only runs on an empty table, existing databases would never gain
    /// the new page — this inserts it (keyed by PageName) under the existing <b>Masters</b> group.
    /// Safe on every startup: existing rows are left untouched.
    /// </summary>
    public static async Task EnsureCompanyDetailsMenuAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Menus.AnyAsync()) return;   // fresh DB — SeedAsync will cover it

        const string page = "masters/company-details";
        if (await db.Menus.AnyAsync(m => m.PageName == page)) return;   // already present

        var masters = await db.Menus.FirstOrDefaultAsync(m => m.ParentId == null && m.MenuName == "Masters");
        if (masters is null) return;

        var nextOrder = (await db.Menus.Where(m => m.ParentId == masters.MenuId)
            .MaxAsync(m => (int?)m.ShowOrder) ?? masters.ShowOrder) + 1;

        db.Menus.Add(new Menu
        {
            MenuName           = "Company Details",
            Icon               = "🏢",
            PageName           = page,
            ParentId           = masters.MenuId,
            ShowOrder          = nextOrder,
            RequiresPermission = true,
            Active             = true,
        });
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
