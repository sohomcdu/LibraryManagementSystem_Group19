using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Mockup 13 – Notification dashboard.</summary>
public class NotificationsModel : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public string? Channel { get; set; }
    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 7;
    [BindProperty(SupportsGet = true)] public int? View { get; set; }

    public int CountBorrow, CountDue, CountOverdue, CountHold;
    public List<NotificationLog> Rows { get; private set; } = new();
    public NotificationLog? Selected { get; private set; }
    public bool ConsoleLogging { get; private set; }

    public async Task OnGetAsync()
    {
        var since = Clock.Now.AddDays(-Days);
        var q = Db.Notifications.Include(n => n.Patron).Where(n => n.CreatedAt >= since);
        CountBorrow = await q.Where(n => n.Type == NotificationType.Borrow).CountAsync();
        CountDue = await q.Where(n => n.Type == NotificationType.DueSoon).CountAsync();
        CountOverdue = await q.Where(n => n.Type == NotificationType.Overdue).CountAsync();
        CountHold = await q.Where(n => n.Type == NotificationType.HoldAvailable).CountAsync();

        if (!string.IsNullOrEmpty(Type) && Enum.TryParse<NotificationType>(Type, out var t)) q = q.Where(n => n.Type == t);
        if (!string.IsNullOrEmpty(Channel) && Enum.TryParse<Channel>(Channel, out var c)) q = q.Where(n => n.Channel == c);
        Rows = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        Selected = View != null ? Rows.FirstOrDefault(r => r.Id == View) ?? await Db.Notifications.FindAsync(View) : Rows.FirstOrDefault();
        ConsoleLogging = (await Svc<SettingsService>().LoadAsync()).ConsoleLogging;
    }

    public async Task<IActionResult> OnPostScanAsync()
    {
        var r = await Svc<NotificationService>().RunScanAsync();
        Flash = $"Scan complete — {r.DueSoon} due-soon, {r.Overdue} overdue notice(s) queued ({r.Skipped} already sent today).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConsoleAsync(bool on)
    {
        await Svc<SettingsService>().SetAsync("ConsoleLogging", on.ToString());
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemindAsync(int loanId)
    {
        var loan = await Db.Loans.Include(l => l.Patron).Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan != null) { await Svc<NotificationService>().RemindAsync(loan); await Db.SaveChangesAsync(); Flash = "Reminder sent."; }
        return RedirectToPage();
    }
}
