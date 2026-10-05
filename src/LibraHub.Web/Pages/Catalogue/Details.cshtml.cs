using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Catalogue;

/// <summary>Mockup 3 – item details, availability by branch, queue banner and the waitlist action.</summary>
public class DetailsModel : AppPageModel
{
    public LibraHub.Services.DetailsModel? View { get; private set; }
    public Reservation? MyHold { get; private set; }
    public int MyPosition { get; private set; }
    public List<Branch> Branches { get; private set; } = new();
    public bool IsPatron => User.IsInRole(nameof(Role.Patron));

    [BindProperty] public int PickupBranchId { get; set; }

    async Task<bool> LoadAsync(int id)
    {
        View = await Svc<CatalogueService>().DetailsAsync(id);
        if (View == null) return false;
        Branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        if (IsPatron)
        {
            MyHold = await Db.Reservations.Include(r => r.PickupBranch).FirstOrDefaultAsync(r => r.PatronId == CurrentUserId &&
                r.Isbn == View.Item.Isbn && (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
            if (MyHold != null) MyPosition = await Svc<ItemLifecycleService>().PositionAsync(MyHold);
        }

        return true;
    }

    public async Task<IActionResult> OnPostReserveAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage(
                "/Account/Login",
                new { returnUrl = $"/Catalogue/Details/{id}" });
        }

        if (!IsPatron)
        {
            FlashError = "Only patrons can reserve items.";
            return RedirectToPage(new { id });
        }

        var item = await Db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound();

        var patron = await Db.Users.FindAsync(CurrentUserId);

        if (patron == null)
            return NotFound();

        Report(
            await Svc<ItemLifecycleService>()
                .JoinWaitlistAsync(
                    patron,
                    item.Isbn,
                    PickupBranchId));

        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        PickupBranchId = User.HomeBranchId() ?? Branches.First().Id;
        return Page();
    }

    public async Task<IActionResult> OnPostJoinAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)      // anonymous → sign in, then return (mockup 1 #9)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Catalogue/Details/{id}" });
        if (!IsPatron) { FlashError = "Only patrons can join a waitlist."; return RedirectToPage(new { id }); }
        var item = await Db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (item == null) return NotFound();
        var patron = await Db.Users.FindAsync(CurrentUserId);
        Report(await Svc<ItemLifecycleService>().JoinWaitlistAsync(patron!, item.Isbn, PickupBranchId));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, int holdId)
    {
        Report(await Svc<ItemLifecycleService>().CancelReservationAsync(holdId, CurrentUserId, CurrentUserName));
        return RedirectToPage(new { id });
    }
}
