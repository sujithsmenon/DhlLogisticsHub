using DhlLogistics.Web.CommonFunctions;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DhlLogistics.Web.Pages.Account;

public class LogoutModel(SignInManager<AppUser> signInManager) : PageModel
{
    // Signing out drops the user back on the public landing site rather than the login
    // form: the landing page is where they came in, and a bare login form gives someone
    // who has just deliberately left no way back to the marketing site.
    public async Task<IActionResult> OnGetAsync()
    {
        await signInManager.SignOutAsync();
        return Redirect(LandingSite.Home);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return Redirect(LandingSite.Home);
    }
}
