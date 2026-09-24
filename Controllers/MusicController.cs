using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Create()
        {
            return View();
        }

        /// POST: Music/Create - saves a new music item after validating the submitted form.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Artist,ReleaseYear,Id,LibraryCode,Name,Description,Status")] Music music)
        {
            if (ModelState.IsValid)
            {
                _context.Add(music);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(music);
        }

        /// GET: Music/Edit/5 - shows the edit form pre-filled with the existing item's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var music = await _context.Music.FindAsync(id);
            if (music == null)
            {
                return NotFound();
            }
            return View(music);
        }

        /// POST: Music/Edit/5 - saves changes to an existing music item, guarding against concurrent edits.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Artist,ReleaseYear,Id,LibraryCode,Name,Description,Status")] Music music)
        {
            if (id != music.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(music);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else deleted this record between the page loading and the save.
                    if (!MusicExists(music.Id))
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
            return View(music);
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
