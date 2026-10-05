using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace LibraHub.Pages.Account;

public class LogoutModel : AppPageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToPage("/Catalogue/Index");
    }

    // Defensive fallback — if anyone hits this URL with a GET (e.g., an old link
    // or a bookmark), sign them out anyway and send them home instead of a 400.
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToPage("/Catalogue/Index");
    }
}