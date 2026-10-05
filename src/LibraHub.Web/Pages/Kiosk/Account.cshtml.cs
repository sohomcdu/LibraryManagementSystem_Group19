using LibraHub.Domain;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LibraHub.Pages.Kiosk;

/// <summary>Mockup 22 – kiosk "My account" hub (loans/holds/fines + entry to check-out).</summary>
public class AccountModel : AppPageModel
{
    public AppUser? Patron { get; private set; }
    public List<Loan> Loans { get; private set; } = new();
    public int Holds { get; private set; }
    public Reservation? NextHold { get; private set; }
    public int FineCents { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var id = HttpContext.Session.GetInt32("kiosk.patron");
        if (id == null) return RedirectToPage("/Kiosk/Index");
        Patron = await Db.Users.FindAsync(id);
        if (Patron == null) return RedirectToPage("/Kiosk/Index");
        Loans = await Db.Loans.Include(l => l.Item).Where(l => l.PatronId == id && l.ReturnedAt == null).OrderBy(l => l.DueAt).ToListAsync();
        Holds = await Db.Reservations.CountAsync(r => r.PatronId == id && (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
        NextHold = await Db.Reservations.Include(r => r.PickupBranch).FirstOrDefaultAsync(r => r.PatronId == id && r.Status == HoldStatus.Ready);
        FineCents = await Svc<FineService>().OutstandingCentsAsync(id.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostSignOutAsync()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToPage("/Kiosk/Activate");
    }
}
