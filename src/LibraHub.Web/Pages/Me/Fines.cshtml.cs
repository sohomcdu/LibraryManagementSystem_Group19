using LibraHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Me;

public class FinesModel : AppPageModel
{
    public List<Fine> Items { get; private set; } = new();
    public int Outstanding { get; private set; }
    public async Task OnGetAsync()
    {
        Outstanding = await Svc<LibraHub.Services.FineService>().OutstandingCentsAsync(CurrentUserId);
        Items = await Db.Fines.Include(f => f.Loan).ThenInclude(l => l.Item)
            .Where(f => f.PatronId == CurrentUserId).OrderByDescending(f => f.IssuedAt).ToListAsync();
    }
}
