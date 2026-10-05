using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Catalogue;

/// <summary>Mockups 1 &amp; 2 – public catalogue search (GET so results are bookmarkable, element #5).</summary>
public class IndexModel : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? Category { get; set; }
    [BindProperty(SupportsGet = true)] public int? Branch { get; set; }
    [BindProperty(SupportsGet = true)] public string? Availability { get; set; }
    [BindProperty(SupportsGet = true)] public List<int> Cats { get; set; } = new();
    [BindProperty(SupportsGet = true)] public List<int> Brs { get; set; } = new();
    [BindProperty(SupportsGet = true)] public List<string> Avs { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "relevance";
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public CatalogueResult Result { get; private set; } = new();
    public List<SelectListItem> CategoryOptions { get; private set; } = new();
    public List<SelectListItem> BranchOptions { get; private set; } = new();
    public bool HasFilters => Category != null || Branch != null || Cats.Count + Brs.Count + Avs.Count > 0
                              || (!string.IsNullOrEmpty(Availability) && Availability != "any");

    public async Task OnGetAsync()
    {
        Result = await Svc<CatalogueService>().SearchAsync(new CatalogueQuery
        {
            Q = Q, Category = Category, Branch = Branch, Availability = Availability, Cats = Cats, Brs = Brs, Avs = Avs, Sort = Sort, PageNo = PageNo
        });
        CategoryOptions = (await Db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync())
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == Category)).ToList();
        BranchOptions = (await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync())
            .Select(b => new SelectListItem(b.Name.Replace(" Library", "").Replace(" Branch", ""), b.Id.ToString(), b.Id == Branch)).ToList();
    }

    /// <summary>Pager link that keeps every active filter (mockup 1 #10).</summary>
    public string PageUrl(int p)
    {
        var qs = new List<KeyValuePair<string, string?>>();
        foreach (var kv in Request.Query.Where(k => k.Key != "p"))
            foreach (var v in kv.Value) qs.Add(new(kv.Key, v));
        qs.Add(new("p", p.ToString()));
        return "/Catalogue" + QueryString.Create(qs).Value;
    }
}
