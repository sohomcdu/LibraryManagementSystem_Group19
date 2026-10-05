using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff.Items;

/// <summary>Mockup 9 – staff item list with bulk transfer / mark damaged.</summary>
public class IndexModel : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Branch { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? Category { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;
    [BindProperty] public List<int> Selected { get; set; } = new();
    [BindProperty] public int ToBranch { get; set; }

    public List<Item> Rows { get; private set; } = new();
    public int Total { get; private set; }
    public const int PageSize = 20;
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();

    public async Task LoadListAsync()
    {
        var q = Db.Items.AsNoTracking().Include(i => i.Category).Include(i => i.CurrentBranch).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var nq = TextUtil.Normalize(Q);
            q = q.Where(i => i.SearchText.Contains(nq));
        }
        if (!string.IsNullOrEmpty(Branch)) q = q.Where(i => i.CurrentBranch.Code == Branch);
        if (!string.IsNullOrEmpty(Status) && Enum.TryParse<ItemStatus>(Status, out var st)) q = q.Where(i => i.Status == st);
        if (Category != null) q = q.Where(i => i.CategoryId == Category);
        Total = await q.CountAsync();
        Rows = await q.OrderBy(i => i.Title).ThenBy(i => i.Code).Skip((PageNo - 1) * PageSize).Take(PageSize).ToListAsync();
        Branches = (await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync()).Select(b => new SelectListItem(b.Name, b.Code, b.Code == Branch)).ToList();
        Categories = (await Db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync()).Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == Category)).ToList();
    }

    public async Task OnGetAsync() => await LoadListAsync();

    public async Task<IActionResult> OnPostTransferAsync()
    {
        var by = CurrentUserName; var restriction = BranchRestriction;
        var r = await Svc<ItemLifecycleService>().BulkTransferAsync(Selected, ToBranch, by, restriction);
        Report(r);
        return RedirectToPage(new { Q, Branch, Status, Category, p = PageNo });
    }

    public async Task<IActionResult> OnPostDamagedAsync()
    {
        int ok = 0;
        foreach (var id in Selected) if ((await Svc<ItemLifecycleService>().MarkDamagedAsync(id, CurrentUserName)).Ok) ok++;
        Flash = $"{ok} item(s) marked damaged.";
        return RedirectToPage(new { Q, Branch, Status, Category, p = PageNo });
    }

    public async Task<IActionResult> OnPostRepairAsync(int id)
    {
        Report(await Svc<ItemLifecycleService>().RepairDoneAsync(id, CurrentUserName));
        return RedirectToPage(new { Q, Branch, Status, Category, p = PageNo });
    }
}
