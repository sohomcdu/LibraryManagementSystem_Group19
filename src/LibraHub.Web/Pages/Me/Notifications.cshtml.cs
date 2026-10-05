using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Me;

public class NotificationsModel : AppPageModel
{
    public List<LibraHub.Domain.NotificationLog> Items { get; private set; } = new();
    public async Task OnGetAsync()
    {
        Items = await Db.Notifications.Where(n => n.PatronId == CurrentUserId).OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
        var unread = Items.Where(n => !n.IsRead).ToList();
        if (unread.Count > 0) { foreach (var n in unread) n.IsRead = true; await Db.SaveChangesAsync(); }
    }
}
