using System.ComponentModel.DataAnnotations;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraHub.Pages.Account;

/// <summary>Mockup 4 – email/password OR library card + PIN (both go through the same lockout rule).</summary>
public class LoginModel : AppPageModel
{
    [BindProperty, EmailAddress] public string? Email { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty] public string? Card { get; set; }
    [BindProperty] public string? Pin { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var (user, error) = await Svc<AuthService>().ValidatePasswordAsync(Email ?? "", Password ?? "");
        if (user == null) { Err = error; return Page(); }
        return await FinishAsync(user);
    }

    public async Task<IActionResult> OnPostCardAsync()
    {
        var (user, error) = await Svc<AuthService>().ValidateCardPinAsync(Card ?? "", Pin ?? "");
        if (user == null) { Err = error; return Page(); }
        return await FinishAsync(user);
    }

    async Task<IActionResult> FinishAsync(AppUser user)
    {
        await Svc<AuthService>().SignInAsync(HttpContext, user, RememberMe);
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
        return RedirectToPage(user.Role switch
        {
            Role.Patron => "/Me/Index",
            Role.Kiosk => "/Kiosk/Index",
            _ => "/Staff/Index"
        });
    }
}
