using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Full CRUD management of Book records for the library catalogue.
    /// Restricted to Admin only - per the assignment brief, Admin manages
    /// items (books, music, toys) and borrower details.
    /// 
    [Authorize(Roles = "Admin")]
    public class BookController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public BookController(LibraryDbContext context)
        {
            _context = context;
        }

        /// GET: Book - lists every book in the catalogue.
        public async Task<IActionResult> Index()
        {
            return View(await _context.Books.ToListAsync());
        }

        /// GET: Book/Details/5 - shows the full details of a single book.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(m => m.Id == id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        /// GET: Book/Create - shows the blank form for adding a new book.
        public async Task<IActionResult> Create()
        {
            await LoadBranchesAsync(null);
            return View();
        }

        /// POST: Book/Create - saves a new book. A branch must be chosen so the item
        /// shows up in branch-scoped checkout, kiosk, search and transfers.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Author,Genre,Id,LibraryCode,Name,Description,Status,BranchId")] Book book)
        {
            // Description is optional; a blank box must not block saving.
            ModelState.Remove(nameof(Book.Description));
            book.Description ??= string.Empty;

            if (book.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == book.BranchId && b.IsActive))
            {
                ModelState.AddModelError(nameof(Book.BranchId), "Please choose the branch that holds this item.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(book.BranchId);
            return View(book);
        }

        /// GET: Book/Edit/5 - shows the edit form pre-filled with the existing book's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (book == null)
            {
                return NotFound();
            }

            await LoadBranchesAsync(book.BranchId);
            return View(book);
        }

        /// POST: Book/Edit/5 - updates only the fields shown on the form. The saved record is
        /// loaded first, so fields that are not on the form (branch, ISBN) are kept, not wiped.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Author,Genre,Id,LibraryCode,Name,Description,Status,BranchId")] Book book)
        {
            if (id != book.Id)
            {
                return NotFound();
            }

            var existing = await _context.Books.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == id);
            if (existing == null)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Book.Description));
            ModelState.Remove(nameof(Book.BranchId));

            // Items that already have a branch keep it (moves go through Item transfers).
            // Items with no branch yet (older data) can be given one here.
            int? newBranchId = existing.BranchId;
            if (existing.BranchId == null)
            {
                if (book.BranchId == null || !await _context.Branches.AnyAsync(b => b.Id == book.BranchId && b.IsActive))
                {
                    ModelState.AddModelError(nameof(Book.BranchId), "Please choose the branch that holds this item.");
                }
                newBranchId = book.BranchId;
            }

            if (!ModelState.IsValid)
            {
                book.Branch = existing.Branch;
                book.BranchId = existing.BranchId;
                await LoadBranchesAsync(newBranchId);
                return View(book);
            }

            existing.Name = book.Name;
            existing.LibraryCode = book.LibraryCode;
            existing.Description = book.Description ?? string.Empty;
            existing.Author = book.Author;
            existing.Genre = book.Genre;
            existing.BranchId = newBranchId;

            // "Reserved" and "InTransit" are set by the hold queue and transfers. The form
            // cannot show them, so keep them instead of letting the form overwrite them.
            if (existing.Status != "Reserved" && existing.Status != "InTransit")
            {
                existing.Status = book.Status;
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

        /// GET: Book/Delete/5 - shows a confirmation page before deleting a book.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(m => m.Id == id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        /// POST: Book/Delete/5 - actually removes the book after the confirmation page is submitted.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// Checks whether a book with the given Id still exists, used to tell a real 404 apart from a concurrency conflict.
        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }
    }
}