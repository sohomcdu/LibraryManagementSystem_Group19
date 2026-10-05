using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Kiosk;

/// <summary>Device sign-in for a shared kiosk terminal (separate from patron sign-in inside the kiosk flow).</summary>
public class ActivateModel : AppPageModel
{
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Password { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var (user, error) = await Svc<AuthService>().ValidatePasswordAsync(Email ?? "", Password ?? "");
        if (user == null || user.Role != Role.Kiosk) { Err = "Unknown kiosk device credentials."; return Page(); }
        await Svc<AuthService>().SignInAsync(HttpContext, user, true);
        return RedirectToPage("/Kiosk/Index");
    }
}
