using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Kiosk;

/// <summary>Mockup 20 – kiosk basket. Uses the SAME LendingService rules as the reception desk (F1).</summary>
public class CheckoutModel : AppPageModel
{
    const string Key = "kiosk.basket";
    [BindProperty] public string? ItemCode { get; set; }
    public AppUser? Patron { get; private set; }
    public List<BasketLine> Basket => HttpContext.Session.GetObj<List<BasketLine>>(Key) ?? new();
    public string? Blocked { get; private set; }

    async Task<AppUser?> LoadAsync()
    {
        var id = HttpContext.Session.GetInt32("kiosk.patron");
        return id == null ? null : await Db.Users.FindAsync(id);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Patron = await LoadAsync();
        if (Patron == null) return RedirectToPage("/Kiosk/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostScanAsync()
    {
        Patron = await LoadAsync();
        if (Patron == null) return RedirectToPage("/Kiosk/Index");

        var branchId = (await Svc<BranchContext>().CurrentAsync()).Id;
        var basket = Basket;

        var r = await Svc<LendingService>().ScanAsync(
            Patron,
            ItemCode ?? "",
            branchId,
            basket.Select(b => b.ItemId).ToList());

        if (r.Error != null)
            Blocked = r.Error;
        else if (!r.Line.Ok)
            Blocked = r.Line.Problem;
        else
        {
            basket.Add(r.Line);
            HttpContext.Session.SetObj(Key, basket);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostFinishAsync()
    {
        Patron = await LoadAsync();
        if (Patron == null) return RedirectToPage("/Kiosk/Index");

        // FIXED: hoist the awaited branch id OUT of the LINQ lambda
        var branchId = (await Svc<BranchContext>().CurrentAsync()).Id;

        var desk = await Db.Desks
            .FirstOrDefaultAsync(d => d.BranchId == branchId && d.IsKiosk);

        if (desk == null)
        {
            Blocked = "No kiosk desk is configured for this branch.";
            return Page();
        }

        var itemIds = Basket.Where(b => b.Ok).Select(b => b.ItemId).ToList();

        var result = await Svc<LendingService>().CheckOutAsync(
            Patron, itemIds, desk.Id, Channels.Email, "Kiosk");

        if (result.Error != null)
        {
            Blocked = result.Error;
            return Page();
        }

        HttpContext.Session.Remove(Key);
        HttpContext.Session.SetInt32("kiosk.lastCount", result.Loans.Count);
        return RedirectToPage("/Kiosk/Receipt");
    }

    public IActionResult OnPostCancel()
    {
        HttpContext.Session.Remove(Key);
        return RedirectToPage("/Kiosk/Account");
    }
}