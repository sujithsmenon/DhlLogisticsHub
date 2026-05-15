namespace DhlLogistics.Web.Api;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            UserManager<AppUser> userManager,
            IConfiguration config) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user is null || !await userManager.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            var roles   = await userManager.GetRolesAsync(user);
            var role    = roles.FirstOrDefault() ?? "Viewer";
            var expires = DateTime.UtcNow.AddDays(7);

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer:             config["Jwt:Issuer"],
                audience:           config["Jwt:Audience"],
                claims:
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email,          user.Email!),
                    new Claim(ClaimTypes.Name,           user.FullName),
                    new Claim(ClaimTypes.Role,           role),
                ],
                expires:            expires,
                signingCredentials: creds);

            return Results.Ok(new LoginResponse
            {
                Token     = new JwtSecurityTokenHandler().WriteToken(token),
                UserId    = user.Id,
                FullName  = user.FullName,
                Role      = role,
                ExpiresAt = expires,
            });
        })
        .AllowAnonymous();
    }
}
