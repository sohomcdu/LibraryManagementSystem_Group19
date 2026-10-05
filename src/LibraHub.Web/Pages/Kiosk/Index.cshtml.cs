using LibraHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraHub.Pages.Kiosk;

/// <summary>Mockup 19 – kiosk welcome / scan card.</summary>
public class IndexModel : AppPageModel
{
    [BindProperty] public string? Card { get; set; }
    public bool TimedOut { get; set; }
    public void OnGet(int? timeout) => TimedOut = timeout == 1;

    public async Task<IActionResult> OnPostAsync()
    {
        var p = await Svc<LibraHub.Services.LendingService>().FindPatronAsync(Card ?? "");
        if (p == null) { Err = "Card not recognised. Try again or ask staff for help."; return Page(); }
        HttpContext.Session.SetInt32("kiosk.patron", p.Id);
        HttpContext.Session.Remove("kiosk.basket");
        return RedirectToPage("/Kiosk/Account");
    }

    [BindProperty] public string? Manual { get; set; }

    public async Task<IActionResult> OnPostManualAsync()
    {
        if (string.IsNullOrWhiteSpace(Manual)) { Err = "Enter card number, name or email."; return Page(); }

        // Try by card number first
        var p = await Svc<LibraHub.Services.LendingService>().FindPatronAsync(Manual.Trim());
        if (p != null)
        {
            HttpContext.Session.SetInt32("kiosk.patron", p.Id);
            HttpContext.Session.Remove("kiosk.basket");
            return RedirectToPage("/Kiosk/Account");
        }

        // Try by email
        p = await Db.Users.FirstOrDefaultAsync(u => u.Email == Manual.Trim());
        if (p != null)
        {
            HttpContext.Session.SetInt32("kiosk.patron", p.Id);
            HttpContext.Session.Remove("kiosk.basket");
            return RedirectToPage("/Kiosk/Account");
        }

        // Fallback: show an informative message so staff can help
        Err = "No matching patron found. Please ask staff for assistance.";
        return Page();
    }

    public IActionResult OnPostHelp()
    {
        // Keep session empty but show message instructing the user to find staff
        Flash = "Please ask a staff member for assistance at the desk.";
        return Page();
    }
}
