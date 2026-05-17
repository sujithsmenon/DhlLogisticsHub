namespace DhlLogistics.Web.Api;

using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Admin-only mirror endpoints for the Users + Permissions screens on the
/// web dashboard. Restricted to Admin role (Manager is read-allowed via the
/// MobileAdminApi policy but these touch identity, so we tighten further).
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("MobileAdminApi");

        // ── Users (Identity) ─────────────────────────────────────────────────
        g.MapGet("/users", async (UserManager<AppUser> users) =>
        {
            var list = await users.Users
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id, u.FullName, u.Email, u.UserName,
                    u.Role, u.IsActive, u.VehicleId, u.CreatedAt,
                })
                .ToListAsync();
            return Results.Ok(list);
        });

        g.MapGet("/users/{id}", async (string id, UserManager<AppUser> users) =>
        {
            var u = await users.FindByIdAsync(id);
            if (u is null) return Results.NotFound();
            var roles = await users.GetRolesAsync(u);
            return Results.Ok(new
            {
                u.Id, u.FullName, u.Email, u.UserName,
                u.Role, u.IsActive, u.VehicleId, u.CreatedAt,
                Roles = roles,
            });
        });

        // ── Roles ────────────────────────────────────────────────────────────
        g.MapGet("/roles", async (RoleManager<IdentityRole> roles) =>
            Results.Ok(await roles.Roles.OrderBy(r => r.Name).Select(r => new { r.Id, r.Name }).ToListAsync()));

        // ── Role-Page-Permission grid (the M3 permissions matrix) ────────────
        g.MapGet("/permissions", async (AppDbContext db) =>
            Results.Ok(await db.RolePagePermissions
                .OrderBy(p => p.RoleId).ThenBy(p => p.PagePath).ThenBy(p => p.Permission)
                .ToListAsync()));

        g.MapGet("/permissions/{roleId}", async (string roleId, AppDbContext db) =>
            Results.Ok(await db.RolePagePermissions
                .Where(p => p.RoleId == roleId)
                .OrderBy(p => p.PagePath).ThenBy(p => p.Permission)
                .ToListAsync()));
    }
}
