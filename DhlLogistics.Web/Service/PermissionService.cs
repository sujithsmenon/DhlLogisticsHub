namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

/// <summary>
/// Mirrors CBM's userService.checkpermision pattern. Caches the user's roles
/// for the lifetime of this scope (per circuit / per request).
/// </summary>
public class PermissionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly Dictionary<string, IList<string>> _roleCache = new();

    public PermissionService(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    public static string Normalise(string pagePath)
    {
        if (string.IsNullOrEmpty(pagePath)) return string.Empty;
        var p = pagePath.Trim('/').ToLowerInvariant();
        // Strip query string if any
        var q = p.IndexOf('?');
        if (q > 0) p = p[..q];
        return p;
    }

    public async Task<bool> CheckPermissionAsync(ClaimsPrincipal? user, string pagePath, Permission perm)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;

        // Admin role bypasses all checks.
        if (user.IsInRole("Admin")) return true;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return false;

        var roles = await GetUserRolesAsync(userId);
        if (roles.Count == 0) return false;

        var path = Normalise(pagePath);
        return await _db.RolePagePermissions
            .AnyAsync(p => roles.Contains(p.RoleId)
                        && p.PagePath == path
                        && p.Permission == perm
                        && p.IsGranted);
    }

    /// <summary>
    /// All page-paths the user may View, used to filter the navigation menu in one
    /// query instead of one round-trip per menu item. Returns <c>null</c> for Admin,
    /// meaning "every page is viewable" (mirrors the Admin bypass in
    /// <see cref="CheckPermissionAsync"/>). Returns an empty set for anonymous /
    /// role-less users.
    /// </summary>
    public async Task<HashSet<string>?> GetViewablePagePathsAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return new();
        if (user.IsInRole("Admin")) return null;   // null = all allowed

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return new();

        var roles = await GetUserRolesAsync(userId);
        if (roles.Count == 0) return new();

        var paths = await _db.RolePagePermissions
            .Where(p => roles.Contains(p.RoleId)
                     && p.Permission == Permission.View
                     && p.IsGranted)
            .Select(p => p.PagePath)
            .Distinct()
            .ToListAsync();

        return paths.ToHashSet();
    }

    /// <summary>All permissions a role has on a given page.</summary>
    public async Task<List<Permission>> GetGrantedPermissionsAsync(string roleId, string pagePath)
    {
        var path = Normalise(pagePath);
        return await _db.RolePagePermissions
            .Where(p => p.RoleId == roleId && p.PagePath == path && p.IsGranted)
            .Select(p => p.Permission)
            .ToListAsync();
    }

    public async Task SetPermissionAsync(string roleId, string pagePath, Permission perm, bool granted, string? updatedBy = null)
    {
        var path = Normalise(pagePath);
        var existing = await _db.RolePagePermissions
            .FirstOrDefaultAsync(p => p.RoleId == roleId && p.PagePath == path && p.Permission == perm);

        if (existing is null)
        {
            _db.RolePagePermissions.Add(new RolePagePermission
            {
                RoleId     = roleId,
                PagePath   = path,
                Permission = perm,
                IsGranted  = granted,
                UpdatedBy  = updatedBy,
                UpdatedAt  = DateTime.UtcNow,
            });
        }
        else
        {
            existing.IsGranted = granted;
            existing.UpdatedBy = updatedBy;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<IList<string>> GetUserRolesAsync(string userId)
    {
        if (_roleCache.TryGetValue(userId, out var cached)) return cached;
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return _roleCache[userId] = Array.Empty<string>();

        // Identity stores role names; map to RoleId via RoleManager — but for our
        // RolePagePermission table the convenient key is the role NAME (e.g. "Manager").
        // CBM uses role IDs (GUIDs). We use NAMES here for readability — IdentityRole.Id == Name in our seed.
        var names = await _users.GetRolesAsync(user);
        return _roleCache[userId] = names;
    }
}
