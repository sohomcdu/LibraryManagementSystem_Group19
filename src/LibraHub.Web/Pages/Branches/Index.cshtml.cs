using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Branches;

public class IndexModel : AppPageModel
{
    public List<Branch> Branches { get; private set; } = new();
    public async Task OnGetAsync() => Branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
}
