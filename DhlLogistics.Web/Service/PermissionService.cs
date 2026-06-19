namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

/// <summary>
/// Claims-based permission checks, ported from CBM. Page permissions are stored as
/// ASP.NET Identity claims with <see cref="PermissionClaimType"/> = "Permission" and
/// a value of <c>Permissions.{page-path}.{action}</c> (e.g.
/// <c>Permissions.masters/users.View</c>), held on either the user's roles
/// (<c>AspNetRoleClaims</c>) or the user directly (<c>AspNetUserClaims</c>).
///
/// The claim is keyed by the normalised page-path (CBM keys by menu name) so the
/// existing per-page checks and <c>NavMenu</c> keep working unchanged. Claims are read
/// live per request, so permission edits take effect on the next navigation without a
/// re-login.
///
/// IMPORTANT: this service opens a SHORT-LIVED context per call via
/// <see cref="IDbContextFactory{AppDbContext}"/> rather than sharing the circuit's
/// scoped <c>AppDbContext</c>. NavMenu (which re-queries on every navigation) and the
/// destination page would otherwise hit the same scoped context concurrently, causing
/// "second operation on this context" / aborted-I/O / ObjectDisposed crashes in Blazor
/// Server.
/// </summary>
public class PermissionService
{
    public const string PermissionClaimType = "Permission";

    private readonly IDbContextFactory<AppDbContext> _factory;

    public PermissionService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public static string Normalise(string pagePath)
    {
        if (string.IsNullOrEmpty(pagePath)) return string.Empty;
        var p = pagePath.Trim('/').ToLowerInvariant();
        var q = p.IndexOf('?');
        if (q > 0) p = p[..q];
        return p;
    }

    /// <summary>Builds the canonical claim value for a (page, action) pair.</summary>
    public static string ClaimValue(string pagePath, Permission perm)
        => $"Permissions.{Normalise(pagePath)}.{perm}";

    public async Task<bool> CheckPermissionAsync(ClaimsPrincipal? user, string pagePath, Permission perm)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;

        // NOTE: Admin is NOT bypassed — every role (including Admin) is governed by the
        // permission tree. The /usermanagement screen itself is role-gated
        // ([Authorize(Roles="Admin")]), so an Admin can always reach it by URL to restore
        // permissions even if they untick their own menu items.
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return false;

        var value = ClaimValue(pagePath, perm);

        await using var db = await _factory.CreateDbContextAsync();

        // 1. User-specific grant.
        if (await db.UserClaims.AnyAsync(c =>
                c.UserId == userId && c.ClaimType == PermissionClaimType && c.ClaimValue == value))
            return true;

        // 2. Grant inherited from any of the user's roles.
        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        if (roleIds.Count == 0) return false;

        return await db.RoleClaims.AnyAsync(rc =>
            roleIds.Contains(rc.RoleId) && rc.ClaimType == PermissionClaimType && rc.ClaimValue == value);
    }

    /// <summary>
    /// All page-paths the user may View (role + user claims), used to filter the
    /// navigation menu in a couple of queries. Returns an empty set for anonymous /
    /// claim-less users. (May still return <c>null</c> in theory — callers treat null as
    /// "all visible" — but no path returns null now that Admin is governed by claims.)
    /// </summary>
    public async Task<HashSet<string>?> GetViewablePagePathsAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return new();

        // Admin is governed by the tree like every other role (no bypass).
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return new();

        const string prefix = "Permissions.";
        const string suffix = ".View";

        await using var db = await _factory.CreateDbContextAsync();

        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var values = new List<string>();

        if (roleIds.Count > 0)
            values.AddRange(await db.RoleClaims
                .Where(rc => roleIds.Contains(rc.RoleId)
                          && rc.ClaimType == PermissionClaimType
                          && rc.ClaimValue!.StartsWith(prefix) && rc.ClaimValue.EndsWith(suffix))
                .Select(rc => rc.ClaimValue!)
                .ToListAsync());

        values.AddRange(await db.UserClaims
            .Where(c => c.UserId == userId
                     && c.ClaimType == PermissionClaimType
                     && c.ClaimValue!.StartsWith(prefix) && c.ClaimValue.EndsWith(suffix))
            .Select(c => c.ClaimValue!)
            .ToListAsync());

        return values
            .Select(v => v.Substring(prefix.Length, v.Length - prefix.Length - suffix.Length))
            .ToHashSet();
    }

    /// <summary>All permissions a role has on a given page (reads role claims).</summary>
    public async Task<List<Permission>> GetGrantedPermissionsAsync(string roleId, string pagePath)
    {
        var prefix = $"Permissions.{Normalise(pagePath)}.";

        await using var db = await _factory.CreateDbContextAsync();
        var values = await db.RoleClaims
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType && rc.ClaimValue!.StartsWith(prefix))
            .Select(rc => rc.ClaimValue!)
            .ToListAsync();

        return values
            .Select(v => v[prefix.Length..])
            .Where(s => Enum.TryParse<Permission>(s, out _))
            .Select(Enum.Parse<Permission>)
            .ToList();
    }

    /// <summary>Grant or revoke a single role claim for a (page, action) pair.</summary>
    public async Task SetPermissionAsync(string roleId, string pagePath, Permission perm, bool granted, string? updatedBy = null)
    {
        var value = ClaimValue(pagePath, perm);

        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.RoleClaims
            .FirstOrDefaultAsync(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType && rc.ClaimValue == value);

        if (granted)
        {
            if (existing is null)
                db.RoleClaims.Add(new IdentityRoleClaim<string>
                {
                    RoleId = roleId,
                    ClaimType = PermissionClaimType,
                    ClaimValue = value,
                });
        }
        else if (existing is not null)
        {
            db.RoleClaims.Remove(existing);
        }

        await db.SaveChangesAsync();
    }
}
