using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DhlLogistics.Web.Pages.Account;

public class LoginModel(SignInManager<AppUser> signInManager) : PageModel
{
    [BindProperty] public string Email      { get; set; } = "";
    [BindProperty] public string Password   { get; set; } = "";
    [BindProperty] public bool   RememberMe { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        var dest = string.IsNullOrEmpty(returnUrl)
            ? "/login"
            : $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Redirect(dest);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var failDest = string.IsNullOrEmpty(returnUrl)
            ? "/login?error=1"
            : $"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}";

        // Identity throws ArgumentNullException if either field is empty/whitespace —
        // short-circuit so an empty form submit just shows the standard "invalid login" error.
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            return Redirect(failDest);

        var result = await signInManager.PasswordSignInAsync(
            Email, Password, RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? "/");

        return Redirect(failDest);
    }
}
