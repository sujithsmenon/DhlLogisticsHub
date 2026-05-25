namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds default permission grants for the four standard roles.
/// Idempotent — only inserts rows that don't already exist (keyed by
/// the unique index on Role+Path+Permission).
/// </summary>
public static class PermissionSeed
{
    /// <summary>List of page paths the permission system covers.</summary>
    private static readonly string[] Pages = new[]
    {
        // CBM-parity stubs
        "masters/branches", "masters/shipment-activities",
        // M1 + M2 masters
        "masters/containers", "masters/vehicles", "masters/transporters", "masters/users", "masters/clients",
        "masters/countries", "masters/regions", "masters/states", "masters/ports", "masters/sez-locations",
        "masters/currencies", "masters/sac", "masters/charge-codes",
        "masters/container-sizes", "masters/commodities", "masters/vessels",
        "masters/vehicle-drivers", "masters/vehicle-document-types", "masters/vehicle-documents", "masters/driver-document-types",
        "masters/staff-departments", "masters/staff-designations", "masters/staff",
        // Operations
        "awb", "export", "jobs",
        "jobs/clearance", "jobs/forwarding", "jobs/verify", "jobs/approve",
        "tracking", "cargo",
        // M4 Billing
        "finance/billing",
        "bills/clearance", "bills/forwarding", "bills/transportation",
        "bills/verify", "bills/approve",
        // M4 Accounts
        "finance/accounts",
        "accounts/heads", "accounts/journal", "accounts/cashbank",
        "accounts/verify", "accounts/approve",
        // Admin
        "admin/permissions",
    };

    private static readonly Permission[] All = Enum.GetValues<Permission>();

    private static readonly Permission[] ReadWrite = new[]
    {
        Permission.View, Permission.Create, Permission.Edit, Permission.Export, Permission.Print
    };

    private static readonly Permission[] ReadOnly = new[]
    {
        Permission.View, Permission.Export, Permission.Print
    };

    private static readonly Permission[] ReadEdit = new[]
    {
        Permission.View, Permission.Edit, Permission.Export, Permission.Print
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        // Admin: PermissionService bypasses checks for the Admin role, but we still
        // seed explicit grants so the matrix admin UI shows everything as checked.
        await GrantAsync(db, "Admin", Pages, All);

        // Manager: full CRUD + Approve on all masters & operations, no admin/permissions
        await GrantAsync(db, "Manager",
            Pages.Where(p => p != "admin/permissions").ToArray(),
            new[] { Permission.View, Permission.Create, Permission.Edit, Permission.Delete,
                    Permission.Approve, Permission.Export, Permission.Print });

        // Executive: View + Edit on masters & operations, no Create/Delete/Approve, no admin
        await GrantAsync(db, "Executive",
            Pages.Where(p => p != "admin/permissions").ToArray(),
            ReadEdit);

        // Viewer: View / Export / Print only on masters & operations
        await GrantAsync(db, "Viewer",
            Pages.Where(p => p != "admin/permissions").ToArray(),
            ReadOnly);

        await db.SaveChangesAsync();
    }

    private static async Task GrantAsync(AppDbContext db, string role, string[] pages, Permission[] perms)
    {
        // Pull existing rows for this role to avoid duplicates.
        var existing = (await db.RolePagePermissions
            .Where(p => p.RoleId == role)
            .Select(p => new { p.PagePath, p.Permission })
            .ToListAsync())
            .Select(x => $"{x.PagePath}|{x.Permission}")
            .ToHashSet();

        foreach (var page in pages)
        foreach (var perm in perms)
        {
            var key = $"{page}|{perm}";
            if (existing.Contains(key)) continue;
            db.RolePagePermissions.Add(new RolePagePermission
            {
                RoleId     = role,
                PagePath   = page,
                Permission = perm,
                IsGranted  = true,
                UpdatedAt  = DateTime.UtcNow,
                UpdatedBy  = "system-seed",
            });
        }
    }
}
