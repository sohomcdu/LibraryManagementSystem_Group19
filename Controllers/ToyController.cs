using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public async Task<IActionResult> Create()
        {
            await LoadBranchesAsync(null);
            return View();
        }

        /// POST: Toy/Create - saves a new toy. A branch must be chosen so the item
        /// shows up in branch-scoped checkout, kiosk, search and transfers.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Type,MinimumAge,Id,LibraryCode,Name,Description,Status,BranchId")] Toy toy)
        {
            // Description is optional; a blank box must not block saving.
            ModelState.Remove(nameof(Toy.Description));
            toy.Description ??= string.Empty;

            if (toy.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == toy.BranchId && b.IsActive))
            {
                ModelState.AddModelError(nameof(Toy.BranchId), "Please choose the branch that holds this item.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(toy);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(toy.BranchId);
            return View(toy);
        }

        /// GET: Toy/Edit/5 - shows the edit form pre-filled with the existing toy's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var toy = await _context.Toys.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (toy == null)
            {
                return NotFound();
            }

            await LoadBranchesAsync(toy.BranchId);
            return View(toy);
        }

        /// POST: Toy/Edit/5 - updates only the fields shown on the form. The saved record is
        /// loaded first, so fields that are not on the form (branch, ISBN) are kept, not wiped.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Type,MinimumAge,Id,LibraryCode,Name,Description,Status,BranchId")] Toy toy)
        {
            if (id != toy.Id)
            {
                return NotFound();
            }

            var existing = await _context.Toys.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Toy.Description));
            ModelState.Remove(nameof(Toy.BranchId));

            // Items that already have a branch keep it (moves go through Item transfers).
            // Items with no branch yet (older data) can be given one here.
            int? newBranchId = existing.BranchId;
            if (existing.BranchId == null)
            {
                if (toy.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == toy.BranchId && b.IsActive))
                {
                    ModelState.AddModelError(nameof(Toy.BranchId), "Please choose the branch that holds this item.");
                }
                newBranchId = toy.BranchId;
            }

            if (!ModelState.IsValid)
            {
                toy.Branch = existing.Branch;
                toy.BranchId = existing.BranchId;
                await LoadBranchesAsync(newBranchId);
                return View(toy);
            }

            existing.Name = toy.Name;
            existing.LibraryCode = toy.LibraryCode;
            existing.Description = toy.Description ?? string.Empty;
            existing.Type = toy.Type;
            existing.MinimumAge = toy.MinimumAge;
            existing.BranchId = newBranchId;

            // "Reserved" and "InTransit" are set by the hold queue and transfers. The form
            // cannot show them, so keep them instead of letting the form overwrite them.
            if (existing.Status != "Reserved" && existing.Status != "InTransit")
            {
                existing.Status = toy.Status;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// Fills the Branch dropdown with active branches.
        private async Task LoadBranchesAsync(int? selectedBranchId)
        {
            var branches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();
            ViewBag.Branches = new SelectList(branches, "Id", "Name", selectedBranchId);
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