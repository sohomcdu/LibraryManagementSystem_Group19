using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Views.Shared.Components.BranchSelector;

public sealed class BranchSelectorViewComponent : ViewComponent
{
    private readonly LibraryDbContext _db;
    public BranchSelectorViewComponent(LibraryDbContext db) => _db = db;
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var branches = await _db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        var selected = HttpContext.Request.Cookies["LMS.BranchCode"];
        if (string.IsNullOrWhiteSpace(selected) || !branches.Any(b => b.Code == selected))
        {
            selected = branches.FirstOrDefault()?.Code;
            if (!string.IsNullOrWhiteSpace(selected)) HttpContext.Response.Cookies.Append("LMS.BranchCode", selected, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true, Secure = HttpContext.Request.IsHttps, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                IsEssential = true, Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        return View(new BranchSelectorViewModel { Branches = branches, SelectedCode = selected });
    }
}
