using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Full CRUD management of Book records for the library catalogue.
    /// Restricted to Admin only - per the assignment brief, Admin manages
    /// items (books, music, toys) but not borrower details, which is
    /// Reception's responsibility instead.
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
        public IActionResult Create()
        {
            return View();
        }

        /// POST: Book/Create - saves a new book after validating the submitted form.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Author,Genre,Id,LibraryCode,Name,Description,Status")] Book book)
        {
            if (ModelState.IsValid)
            {
                _context.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(book);
        }

        /// GET: Book/Edit/5 - shows the edit form pre-filled with the existing book's data.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }
            return View(book);
        }

        /// POST: Book/Edit/5 - saves changes to an existing book, guarding against concurrent edits.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Author,Genre,Id,LibraryCode,Name,Description,Status")] Book book)
        {
            if (id != book.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else deleted this record between the page loading and the save.
                    if (!BookExists(book.Id))
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
            return View(book);
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
