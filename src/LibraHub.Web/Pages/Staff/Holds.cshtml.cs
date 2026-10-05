using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Mockup 12 – Holds & waitlist queue.</summary>
public class HoldsModel : AppPageModel
{
    public record WaitlistedTitle(string Isbn, string Title, ItemStatus RepStatus, int Queue);
    public List<WaitlistedTitle> Titles { get; private set; } = new();
    public List<Reservation> Ready { get; private set; } = new();
    public string? SelectedIsbn { get; private set; }
    public List<(int Pos, Reservation R)> Queue { get; private set; } = new();
    public LibrarySettings Settings { get; private set; } = null!;

    [BindProperty] public int MaxHolds { get; set; }
    [BindProperty] public int PickupDays { get; set; }

    public async Task OnGetAsync(string? isbn)
    {
        var branchId = await StaffBranchIdAsync();
        Settings = await Svc<SettingsService>().LoadAsync();
        MaxHolds = Settings.MaxHolds; PickupDays = Settings.PickupDays;

        var waiting = await Db.Reservations.Where(r => r.Status == HoldStatus.Waiting).ToListAsync();
        var isbns = waiting.Select(r => r.Isbn).Distinct().ToList();
        var reps = await Db.Items.Where(i => isbns.Contains(i.Isbn)).ToListAsync();
        Titles = isbns.Select(x =>
        {
            var copies = reps.Where(r => r.Isbn == x).ToList();
            var rep = copies.FirstOrDefault(c => c.Status == ItemStatus.Reserved) ?? copies.OrderBy(c => c.Status).FirstOrDefault();
            return new WaitlistedTitle(x, copies.FirstOrDefault()?.Title ?? waiting.First(w => w.Isbn == x).TitleText,
                rep?.Status ?? ItemStatus.Borrowed, waiting.Count(w => w.Isbn == x));
        }).OrderByDescending(t => t.Queue).ToList();

        Ready = await Db.Reservations.Include(r => r.Patron).Where(r => r.Status == HoldStatus.Ready && r.PickupBranchId == branchId)
            .OrderBy(r => r.ExpiresAt).ToListAsync();

        SelectedIsbn = isbn ?? Titles.FirstOrDefault()?.Isbn;
        if (SelectedIsbn != null)
        {
            var list = await Db.Reservations.Include(r => r.Patron).Where(r => r.Isbn == SelectedIsbn && r.Status == HoldStatus.Waiting)
                .OrderBy(r => r.QueueKey).ToListAsync();
            Queue = list.Select((r, i) => (i + 1, r)).ToList();
        }
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? isbn)
    {
        Report(await Svc<ItemLifecycleService>().CancelReservationAsync(id, null, CurrentUserName));
        return RedirectToPage(new { isbn });
    }

    public async Task<IActionResult> OnPostFrontAsync(int id, string? isbn)
    {
        Report(await Svc<ItemLifecycleService>().MoveToFrontAsync(id, CurrentUserName));
        return RedirectToPage(new { isbn });
    }

    public async Task<IActionResult> OnPostPickedUpAsync(int id)
    {
        Report(await Svc<ItemLifecycleService>().MarkPickedUpAsync(id));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSettingsAsync()
    {
        if (!IsManagerUp) return Forbid();
        await Svc<SettingsService>().SetAsync("MaxHolds", MaxHolds.ToString());
        await Svc<SettingsService>().SetAsync("PickupDays", PickupDays.ToString());
        Flash = "Waitlist settings saved.";
        return RedirectToPage();
    }
}
