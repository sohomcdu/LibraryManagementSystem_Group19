using System.ComponentModel.DataAnnotations;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Account;

/// <summary>Mockup 4 – patron self-registration; issues a library card number immediately.</summary>
public class RegisterModel : AppPageModel
{
    [BindProperty, Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty] public string? Mobile { get; set; }
    [BindProperty, Required, MinLength(8)] public string Password { get; set; } = "";
    [BindProperty] public bool NotifyEmail { get; set; } = true;
    [BindProperty] public bool NotifySms { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { Err = "Please check the highlighted fields."; return Page(); }
        var email = Email.Trim().ToLowerInvariant();
        if (await Db.Users.AnyAsync(u => u.Email == email)) { Err = "An account with this email already exists."; return Page(); }
        if (NotifySms && string.IsNullOrWhiteSpace(Mobile)) { Err = "Add a mobile number to enable SMS notifications."; return Page(); }

        var central = await Db.Branches.OrderBy(b => b.Id).FirstAsync();
        var card = "LC-" + Random.Shared.Next(1000, 9999) + "-" + Random.Shared.Next(1000, 9999);
        var user = new AppUser
        {
            Email = email, FullName = FullName.Trim(), Mobile = Mobile, Role = Role.Patron, HomeBranch = central, CardNumber = card,
            PrefBorrow = NotifyEmail ? Channels.Email : Channels.None, PrefDueSoon = Combine(NotifyEmail, NotifySms),
            PrefOverdue = Combine(NotifyEmail, NotifySms), PrefHold = Combine(NotifyEmail, NotifySms)
        };
        var hasher = Svc<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>();
        user.PasswordHash = hasher.HashPassword(user, Password);
        user.PinHash = hasher.HashPassword(user, "0000");
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        await Svc<AuthService>().SignInAsync(HttpContext, user, true);
        Flash = $"Welcome, {user.FullName}! Your library card number is {card}.";
        return RedirectToPage("/Me/Index");
    }

    static Channels Combine(bool email, bool sms) => (email ? Channels.Email : 0) | (sms ? Channels.Sms : 0);
}
