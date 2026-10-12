using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Full CRUD management of Toy records for the library catalogue.
    /// Restricted to Admin only, matching BookController and MusicController.
    /// 
    [Authorize(Roles = "Admin")]
    public class ToyController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public ToyController(LibraryDbContext context)
        {
            _context = context;
        }

        /// GET: Toy - lists every toy in the catalogue.
        public async Task<IActionResult> Index()
        {
            return View(await _context.Toys.ToListAsync());
        }

        /// GET: Toy/Details/5 - shows the full details of a single toy.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var toy = await _context.Toys
                .FirstOrDefaultAsync(m => m.Id == id);
            if (toy == null)
            {
                return NotFound();
            }

            return View(toy);
        }

        /// GET: Toy/Create - shows the blank form for adding a new toy.
        public IActionResult Create()
        {
            return View();
        }

        /// POST: Toy/Create - saves a new toy after validating the submitted form.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Type,MinimumAge,Id,LibraryCode,Name,Description,Status")] Toy toy)
        {
            if (ModelState.IsValid)
            {
                _context.Add(toy);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(toy);
        }

        /// GET: Toy/Edit/5 - shows the edit form pre-filled with the existing toy's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var toy = await _context.Toys.FindAsync(id);
            if (toy == null)
            {
                return NotFound();
            }
            return View(toy);
        }

        /// POST: Toy/Edit/5 - saves changes to an existing toy, guarding against concurrent edits.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Type,MinimumAge,Id,LibraryCode,Name,Description,Status")] Toy toy)
        {
            if (id != toy.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(toy);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else deleted this record between the page loading and the save.
                    if (!ToyExists(toy.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(toy);
        }

        /// GET: Toy/Delete/5 - shows a confirmation page before deleting a toy.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var toy = await _context.Toys
                .FirstOrDefaultAsync(m => m.Id == id);
            if (toy == null)
            {
                return NotFound();
            }

            return View(toy);
        }

        /// POST: Toy/Delete/5 - actually removes the toy after the confirmation page is submitted.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var toy = await _context.Toys.FindAsync(id);
            if (toy != null)
            {
                _context.Toys.Remove(toy);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// Checks whether a toy with the given Id still exists, used to tell a real 404 apart from a concurrency conflict.
        private bool ToyExists(int id)
        {
            return _context.Toys.Any(e => e.Id == id);
        }
    }
}
