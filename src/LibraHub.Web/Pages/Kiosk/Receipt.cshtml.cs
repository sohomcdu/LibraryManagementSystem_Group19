using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace LibraHub.Pages.Kiosk;
public class ReceiptModel : AppPageModel
{
    public LibraHub.Domain.AppUser? Patron { get; private set; }
    public List<LibraHub.Domain.Loan> Loans { get; private set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        var id = HttpContext.Session.GetInt32("kiosk.patron");
        if (id == null) return RedirectToPage("/Kiosk/Index");
        Patron = await Db.Users.FindAsync(id);
        Loans = await Db.Loans.Include(l => l.Item).Where(l => l.PatronId == id && l.ReturnedAt == null)
            .OrderByDescending(l => l.BorrowedAt).Take(HttpContext.Session.GetInt32("kiosk.lastCount") ?? 1).ToListAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostDoneAsync()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToPage("/Kiosk/Activate");
    }
}
