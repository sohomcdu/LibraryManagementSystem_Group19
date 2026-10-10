using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    /// <summary>Public, read-only catalogue search across books, music and toys.</summary>
    [AllowAnonymous]
    public class SearchController : Controller
    {
        private readonly LibraryDbContext _context;

        public SearchController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index(string? query, string? branchCode, string? itemType, string? status, string? category)
        {
            ViewData["Query"] = query;
            ViewData["BranchCode"] = branchCode;
            ViewData["ItemType"] = itemType;
            ViewData["StatusFilter"] = status;
            ViewData["Category"] = category;
            ViewData["Branches"] = await _context.Branches.AsNoTracking()
                .Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();

            IQueryable<Item> items = _context.Items.Include(i => i.Branch).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(branchCode))
                items = items.Where(i => i.Branch != null && i.Branch.Code == branchCode);

            if (!string.IsNullOrWhiteSpace(itemType))
            {
                switch (itemType.Trim().ToLowerInvariant())
                {
                    case "book": items = items.OfType<Book>(); break;
                    case "music": items = items.OfType<Music>(); break;
                    case "toy": items = items.OfType<Toy>(); break;
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
                items = items.Where(i => i.Status == status);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                items = items.Where(i => i.Name.Contains(q) || i.LibraryCode.Contains(q) || i.Description.Contains(q));
            }

            // Search the type-specific metadata as well as common fields.
            if (!string.IsNullOrWhiteSpace(category))
            {
                var c = category.Trim();
                items = items.Where(i =>
                    (i is Book && ((Book)i).Genre.Contains(c)) ||
                    (i is Music && ((Music)i).Artist.Contains(c)) ||
                    (i is Toy && ((Toy)i).Type.Contains(c)));
            }

            var results = await items.OrderBy(i => i.Name).ToListAsync();
            ViewData["ResultCount"] = results.Count;
            return View(results);
        }
    }
}
