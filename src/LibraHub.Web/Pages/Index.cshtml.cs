using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages;

/// <summary>The public homepage: a welcome banner, branch locations, what the library offers, and a
/// quick search box that hands off to the full catalogue search (Catalogue/Index).</summary>
public class IndexModel : AppPageModel
{
    public List<Branch> Branches { get; private set; } = new();
    public int TotalItems { get; private set; }
    public int TotalTitles { get; private set; }
    public bool SignedIn => User.Identity?.IsAuthenticated == true;
    public string? FirstName => SignedIn ? CurrentUserName.Split(' ')[0] : null;

    public async Task OnGetAsync()
    {
        Branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        TotalItems = await Db.Items.CountAsync();
        TotalTitles = await Db.Items.Select(i => i.Isbn).Distinct().CountAsync();
    }
}
