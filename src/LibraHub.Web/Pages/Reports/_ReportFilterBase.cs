using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Reports;

/// <summary>Shared date-range/branch/category filter bar used by all three report tabs.</summary>
public abstract class ReportPageBase : AppPageModel
{
    public string FromStr { get; set; } = Clock.Today.AddDays(-54).ToString("yyyy-MM-dd");
    public string ToStr { get; set; } = Clock.Today.ToString("yyyy-MM-dd");
    public int? BranchId { get; set; }
    public int? CategoryId { get; set; }
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();
    public ReportFilter Filter { get; private set; } = new();

    protected async Task BindFilterAsync(string? from, string? to, int? branch, int? category)
    {
        if (DateTime.TryParse(from, out var f)) FromStr = f.ToString("yyyy-MM-dd");
        if (DateTime.TryParse(to, out var t)) ToStr = t.ToString("yyyy-MM-dd");
        BranchId = branch; CategoryId = category;
        Filter = new ReportFilter { From = DateTime.Parse(FromStr), To = DateTime.Parse(ToStr), BranchId = branch, CategoryId = category };
        Branches = (await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync()).Select(b => new SelectListItem(b.Name, b.Id.ToString(), b.Id == branch)).ToList();
        Categories = (await Db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync()).Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == category)).ToList();
    }

    public string Qs(string extra = "") =>
        $"from={FromStr}&to={ToStr}{(BranchId != null ? $"&branch={BranchId}" : "")}{(CategoryId != null ? $"&category={CategoryId}" : "")}{extra}";
}
