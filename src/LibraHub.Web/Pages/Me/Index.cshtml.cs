using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Me;

/// <summary>Mockup 5 – patron dashboard KPIs + current loans + recent notifications.</summary>
public class IndexModel : AppPageModel
{
    public AppUser Patron { get; private set; } = null!;
    public List<Loan> Loans { get; private set; } = new();
    public int Holds { get; private set; }
    public int ReadyHolds { get; private set; }
    public int FineCents { get; private set; }
    public int OverdueCount { get; private set; }
    public int UnreadCount { get; private set; }
    public List<NotificationLog> Recent { get; private set; } = new();
    [BindProperty] public List<int> SelectedLoans { get; set; } = new();

    async Task LoadAsync()
    {
        Patron = await Db.Users.Include(u => u.HomeBranch).FirstAsync(u => u.Id == CurrentUserId);
        Loans = await Db.Loans.Include(l => l.Item).ThenInclude(i => i.CurrentBranch)
            .Where(l => l.PatronId == CurrentUserId && l.ReturnedAt == null).OrderBy(l => l.DueAt).ToListAsync();
        OverdueCount = Loans.Count(l => l.DueAt.Date < Clock.Today);
        Holds = await Db.Reservations.CountAsync(r => r.PatronId == CurrentUserId && (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
        ReadyHolds = await Db.Reservations.CountAsync(r => r.PatronId == CurrentUserId && r.Status == HoldStatus.Ready);
        FineCents = await Svc<FineService>().OutstandingCentsAsync(CurrentUserId);
        UnreadCount = await Db.Notifications.CountAsync(n => n.PatronId == CurrentUserId && !n.IsRead);
        Recent = await Db.Notifications.Where(n => n.PatronId == CurrentUserId).OrderByDescending(n => n.CreatedAt).Take(3).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostRenewAsync()
    {
        var svc = Svc<ItemLifecycleService>();
        int ok = 0; string? lastErr = null;
        foreach (var id in SelectedLoans)
        {
            var r = await svc.RenewAsync(id, CurrentUserId);
            if (r.Ok) ok++; else lastErr = r.Message;
        }
        if (SelectedLoans.Count == 0) FlashError = "Select at least one loan to renew.";
        else if (ok == SelectedLoans.Count) Flash = $"{ok} item(s) renewed.";
        else { Flash = $"{ok} item(s) renewed."; FlashError = lastErr; }
        return RedirectToPage();
    }
}
