using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Full CRUD management of Music records for the library catalogue.
    /// Restricted to Admin only, matching BookController and ToyController.
    /// 
    [Authorize(Roles = "Admin")]
    public class MusicController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public MusicController(LibraryDbContext context)
        {
            _context = context;
        }

        /// GET: Music - lists every music item in the catalogue.
        public async Task<IActionResult> Index()
        {
            return View(await _context.Music.ToListAsync());
        }

        /// GET: Music/Details/5 - shows the full details of a single music item.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var music = await _context.Music
                .FirstOrDefaultAsync(m => m.Id == id);
            if (music == null)
            {
                return NotFound();
            }

            return View(music);
        }

        /// GET: Music/Create - shows the blank form for adding a new music item.
        public async Task<IActionResult> Create()
        {
            await LoadBranchesAsync(null);
            return View();
        }

        /// POST: Music/Create - saves a new music item. A branch must be chosen so the item
        /// shows up in branch-scoped checkout, kiosk, search and transfers.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Artist,ReleaseYear,Id,LibraryCode,Name,Description,Status,BranchId")] Music music)
        {
            // Description is optional; a blank box must not block saving.
            ModelState.Remove(nameof(Music.Description));
            music.Description ??= string.Empty;

            if (music.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == music.BranchId && b.IsActive))
            {
                ModelState.AddModelError(nameof(Music.BranchId), "Please choose the branch that holds this item.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(music);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(music.BranchId);
            return View(music);
        }

        /// GET: Music/Edit/5 - shows the edit form pre-filled with the existing music item's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var music = await _context.Music.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (music == null)
            {
                return NotFound();
            }

            await LoadBranchesAsync(music.BranchId);
            return View(music);
        }

        /// POST: Music/Edit/5 - updates only the fields shown on the form. The saved record is
        /// loaded first, so fields that are not on the form (branch, ISBN) are kept, not wiped.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Artist,ReleaseYear,Id,LibraryCode,Name,Description,Status,BranchId")] Music music)
        {
            if (id != music.Id)
            {
                return NotFound();
            }

            var existing = await _context.Music.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Music.Description));
            ModelState.Remove(nameof(Music.BranchId));

            // Items that already have a branch keep it (moves go through Item transfers).
            // Items with no branch yet (older data) can be given one here.
            int? newBranchId = existing.BranchId;
            if (existing.BranchId == null)
            {
                if (music.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == music.BranchId && b.IsActive))
                {
                    ModelState.AddModelError(nameof(Music.BranchId), "Please choose the branch that holds this item.");
                }
                newBranchId = music.BranchId;
            }

            if (!ModelState.IsValid)
            {
                music.Branch = existing.Branch;
                music.BranchId = existing.BranchId;
                await LoadBranchesAsync(newBranchId);
                return View(music);
            }

            existing.Name = music.Name;
            existing.LibraryCode = music.LibraryCode;
            existing.Description = music.Description ?? string.Empty;
            existing.Artist = music.Artist;
            existing.ReleaseYear = music.ReleaseYear;
            existing.BranchId = newBranchId;

            // "Reserved" and "InTransit" are set by the hold queue and transfers. The form
            // cannot show them, so keep them instead of letting the form overwrite them.
            if (existing.Status != "Reserved" && existing.Status != "InTransit")
            {
                existing.Status = music.Status;
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

        /// GET: Music/Delete/5 - shows a confirmation page before deleting a music item.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var music = await _context.Music
                .FirstOrDefaultAsync(m => m.Id == id);
            if (music == null)
            {
                return NotFound();
            }

            return View(music);
        }

        /// POST: Music/Delete/5 - actually removes the music item after the confirmation page is submitted.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var music = await _context.Music.FindAsync(id);
            if (music != null)
            {
                _context.Music.Remove(music);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// Checks whether a music item with the given Id still exists, used to tell a real 404 apart from a concurrency conflict.
        private bool MusicExists(int id)
        {
            return _context.Music.Any(e => e.Id == id);
        }
    }
}