namespace DhlLogistics.Web.Service;

using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

/// <summary>
/// Builds the cookie principal for a signed-in user, then strips every "Permission"
/// claim from it.
///
/// ASP.NET Identity's default factory copies BOTH the user's claims
/// (<c>AspNetUserClaims</c>) and the claims of the user's roles
/// (<c>AspNetRoleClaims</c>) into the authentication cookie. With claims-based page
/// permissions there can be thousands of these, which blows the auth cookie past the
/// HTTP header size limit — the server rejects the next request with 431 (and an AWS
/// ALB surfaces it as 502).
///
/// Permission claims are evaluated live from the database by
/// <see cref="PermissionService"/>, so they never need to live in the cookie. Role
/// NAME claims are preserved, so <c>IsInRole</c> / <c>[Authorize(Roles=…)]</c> still work.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    public override async Task<ClaimsPrincipal> CreateAsync(AppUser user)
    {
        var principal = await base.CreateAsync(user);

        if (principal.Identity is ClaimsIdentity identity)
        {
            foreach (var claim in identity.FindAll(PermissionService.PermissionClaimType).ToList())
                identity.RemoveClaim(claim);
        }

        return principal;
    }
}
