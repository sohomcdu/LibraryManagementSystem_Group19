using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Public, read-only catalogue search - the "portal where the public can
    /// search for items online from their phone" requirement. No login
    /// required and no CRUD links are exposed here; this is a search
    /// experience, not an admin page.
    /// 
    [AllowAnonymous]
    public class SearchController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public SearchController(LibraryDbContext context)
        {
            _context = context;
        }

        /// 
        /// GET: Search - returns every item if no query is supplied, or
        /// filters by name/library code/description if one is. Queries the
        /// abstract Item DbSet directly, so Books, Music, and Toys are all
        /// searched together in a single pass rather than three separate
        /// queries.
        /// 
        public async Task<IActionResult> Index(string? query)
        {
            ViewData["Query"] = query;

            IQueryable<Item> items = _context.Items;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                items = items.Where(i =>
                    i.Name.Contains(q) ||
                    i.LibraryCode.Contains(q) ||
                    i.Description.Contains(q));
            }

            var results = await items
                .OrderBy(i => i.Name)
                .ToListAsync();

            return View(results);
        }
    }
}
