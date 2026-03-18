using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FamilyWebBlazorServer.Pages.Account;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        // Render a confirmation form. Logout is performed via POST to prevent CSRF.
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/account/login");
    }
}
