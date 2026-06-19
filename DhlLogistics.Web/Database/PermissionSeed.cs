namespace DhlLogistics.Web.Database;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds default page permissions for the four standard roles as ASP.NET Identity
/// role claims (<c>AspNetRoleClaims</c>, claim type "Permission"), and migrates any
/// existing legacy <see cref="RolePagePermission"/> grants into the same claim format
/// so nothing regresses when the permission backend switches to claims.
///
/// Idempotent — only inserts claims that don't already exist. Role claims are keyed by
/// the role's IdentityRole.Id (GUID); the legacy table keyed grants by role NAME, so
/// the converter maps name → id.
/// </summary>
public static class PermissionSeed
{
    /// <summary>List of page paths the permission system covers.</summary>
    private static readonly string[] Pages = new[]
    {
        // CBM-parity stubs
        "masters/branches", "masters/shipment-activities",
        // M1 + M2 masters
        "masters/containers", "masters/vehicles", "masters/transporters", "masters/clients",
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
        // Finance Reports
        "reports/finance", "reports/ledger", "reports/trial-balance",
        "reports/gst-output", "reports/bill-register",
        // Admin (CBM-style user management replaces the old matrix page)
        "usermanagement",
    };

    private static readonly Permission[] All = Enum.GetValues<Permission>();

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
        var roleIdByName = await db.Roles
            .Where(r => r.Name != null)
            .ToDictionaryAsync(r => r.Name!, r => r.Id);

        // 1. Migrate any legacy RolePagePermission grants into role claims, then persist
        //    so the "already has claims?" checks below see them.
        await ConvertLegacyAsync(db, roleIdByName);
        await db.SaveChangesAsync();

        // 2. First-time-only default claims. We seed a role's defaults ONLY when it has no
        //    permission claims yet. Otherwise every restart would re-add defaults and
        //    silently undo permissions an admin unticked in the tree.
        var nonAdminPages = Pages.Where(p => p != "usermanagement").ToArray();

        await SeedDefaultsIfEmptyAsync(db, roleIdByName, "Admin", Pages, All);
        await SeedDefaultsIfEmptyAsync(db, roleIdByName, "Manager", nonAdminPages,
            new[] { Permission.View, Permission.Create, Permission.Edit, Permission.Delete,
                    Permission.Approve, Permission.Export, Permission.Print });
        await SeedDefaultsIfEmptyAsync(db, roleIdByName, "Executive", nonAdminPages, ReadEdit);
        await SeedDefaultsIfEmptyAsync(db, roleIdByName, "Viewer", nonAdminPages, ReadOnly);

        await db.SaveChangesAsync();
    }

    private static async Task SeedDefaultsIfEmptyAsync(
        AppDbContext db, IReadOnlyDictionary<string, string> roleIdByName,
        string roleName, string[] pages, Permission[] perms)
    {
        if (!roleIdByName.TryGetValue(roleName, out var roleId)) return;

        var hasClaims = await db.RoleClaims
            .AnyAsync(rc => rc.RoleId == roleId && rc.ClaimType == PermissionService.PermissionClaimType);
        if (hasClaims) return;   // already seeded / converted / hand-edited — never override

        await GrantClaimsAsync(db, roleId, pages, perms);
    }

    /// <summary>
    /// Copies granted legacy rows into role claims. Skipped silently for any row whose
    /// role name no longer maps to an IdentityRole.
    /// </summary>
    private static async Task ConvertLegacyAsync(AppDbContext db, IReadOnlyDictionary<string, string> roleIdByName)
    {
        // The legacy table may not exist on brand-new databases; tolerate that.
        List<RolePagePermission> legacy;
        try
        {
            legacy = await db.RolePagePermissions.Where(p => p.IsGranted).ToListAsync();
        }
        catch
        {
            return;
        }
        if (legacy.Count == 0) return;

        foreach (var grp in legacy.GroupBy(p => p.RoleId))
        {
            if (!roleIdByName.TryGetValue(grp.Key, out var roleId)) continue;

            var existing = await ExistingClaimsAsync(db, roleId);
            foreach (var row in grp)
            {
                var value = PermissionService.ClaimValue(row.PagePath, row.Permission);
                if (existing.Add(value))
                    db.RoleClaims.Add(new IdentityRoleClaim<string>
                    {
                        RoleId = roleId,
                        ClaimType = PermissionService.PermissionClaimType,
                        ClaimValue = value,
                    });
            }
        }
    }

    private static async Task GrantClaimsAsync(AppDbContext db, string roleId, string[] pages, Permission[] perms)
    {
        var existing = await ExistingClaimsAsync(db, roleId);
        foreach (var page in pages)
        foreach (var perm in perms)
        {
            var value = PermissionService.ClaimValue(page, perm);
            if (existing.Add(value))
                db.RoleClaims.Add(new IdentityRoleClaim<string>
                {
                    RoleId = roleId,
                    ClaimType = PermissionService.PermissionClaimType,
                    ClaimValue = value,
                });
        }
    }

    private static async Task<HashSet<string>> ExistingClaimsAsync(AppDbContext db, string roleId) =>
        (await db.RoleClaims
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionService.PermissionClaimType)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync())
        .ToHashSet();
}
