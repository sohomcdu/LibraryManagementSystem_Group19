using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Me;

public class ReservationsModel : AppPageModel
{
    public List<Reservation> Items { get; private set; } = new();
    public List<Branch> Branches { get; private set; } = new();
    public Dictionary<int, int> Positions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await Db.Reservations.Include(r => r.PickupBranch).Include(r => r.ReadyItem)
            .Where(r => r.PatronId == CurrentUserId).OrderByDescending(r => r.PlacedAt).ToListAsync();
        Branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        var svc = Svc<ItemLifecycleService>();
        foreach (var r in Items.Where(r => r.Status == HoldStatus.Waiting)) Positions[r.Id] = await svc.PositionAsync(r);
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        Report(await Svc<ItemLifecycleService>().CancelReservationAsync(id, CurrentUserId, CurrentUserName));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBranchAsync(int id, int branchId)
    {
        Report(await Svc<ItemLifecycleService>().ChangePickupBranchAsync(id, CurrentUserId, branchId));
        return RedirectToPage();
    }
}
