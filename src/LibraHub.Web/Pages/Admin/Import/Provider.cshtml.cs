using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Admin.Import;

/// <summary>Mockup 15 – mock metadata provider search + import.</summary>
public class ProviderModel : AppPageModel
{
    [BindProperty] public string? Query { get; set; }
    [BindProperty] public List<string> Selected { get; set; } = new();
    [BindProperty] public int BranchId { get; set; }
    [BindProperty] public int Copies { get; set; } = 1;
    [BindProperty] public Dictionary<string, int> CategoryFor { get; set; } = new();

    public List<ProviderRecord> Results { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<SelectListItem> Categories { get; private set; } = new();
    public List<ImportJob> Recent { get; private set; } = new();
    public string ProviderName { get; private set; } = "";

    public async Task OnGetAsync()
    {
        Branches = (await Db.Branches.OrderBy(b => b.Id).ToListAsync()).Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();
        Categories = (await Db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        BranchId = Branches.FirstOrDefault()?.Value is string s ? int.Parse(s) : 0;
        Recent = await Db.ImportJobs.OrderByDescending(j => j.Id).Take(5).ToListAsync();
        ProviderName = Svc<IMetadataProvider>().Name;
    }

    public async Task<IActionResult> OnPostSearchAsync()
    {
        await OnGetAsync();
        Results = (await Svc<IMetadataProvider>().SearchAsync(Query ?? "")).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        await OnGetAsync();
        var all = await Svc<IMetadataProvider>().SearchAsync(Query ?? "");
        var uncategorised = (await Db.Categories.FirstAsync(c => c.Name == "Uncategorised")).Id;
        var picks = Selected.Select(isbn => all.FirstOrDefault(r => r.Isbn == isbn))
            .Where(r => r != null).Select(r => (r!, CategoryFor.GetValueOrDefault(r!.Isbn, uncategorised))).ToList();
        if (picks.Count == 0) { Err = "Select at least one result."; Results = all.ToList(); return Page(); }
        var job = await Svc<ImportService>().ImportProviderAsync(picks, BranchId, Copies, ProviderName, CurrentUserName);
        Flash = $"Imported {job.Imported} title(s) ({job.Skipped} already in catalogue).";
        return RedirectToPage();
    }
}
