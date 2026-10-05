using Microsoft.EntityFrameworkCore;
namespace LibraHub.Pages.Admin.Import;
public class HistoryModel : AppPageModel
{
    public List<LibraHub.Domain.ImportJob> Jobs { get; private set; } = new();
    public async Task OnGetAsync() => Jobs = await Db.ImportJobs.OrderByDescending(j => j.Id).ToListAsync();
}
