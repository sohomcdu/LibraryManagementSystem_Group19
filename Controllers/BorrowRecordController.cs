using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Handles Reception's core workflow: lending items out, correcting
    /// loan dates, and processing returns with automatic late fines.
    /// Available to Reception and Admin (unlike Borrower CRUD, which is
    /// Reception-only) since Admin may reasonably need to assist with loans.
    /// 
    [Authorize(Roles = "Reception,Admin")]
    public class BorrowRecordController : Controller
    {
        /// 
        /// The fine charged per day an item is returned late. Declared as a
        /// public constant so ManagerController can reuse the exact same
        /// rate when projecting fines still accruing on overdue loans, so
        /// the two never drift out of sync.
        /// 
        public const decimal DailyFineRate = 1.00m;

        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public BorrowRecordController(LibraryDbContext context)
        {
            _context = context;
        }

        /// GET: BorrowRecord - lists every loan (active and completed), with item and borrower details loaded.
        public async Task<IActionResult> Index()
        {
            var records = await _context.BorrowRecords
                .Include(b => b.Item)
                .Include(b => b.Borrower)
                .ToListAsync();

            return View(records);
        }

        /// GET: BorrowRecord/Create - shows the lending form, with the item dropdown limited to items currently on the shelf.
        public IActionResult Create()
        {
            // Only items sitting on the shelf can be borrowed.
            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available"), "Id", "Name");
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName");
            return View();
        }

        /// 
        /// POST: BorrowRecord/Create - creates a new loan and flips the item's
        /// status to "Borrowed". Re-checks the item's availability server-side
        /// (not just trusting the dropdown), so a stale page or a tampered
        /// request can never double-lend an item.
        /// 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ItemId,BorrowerId,DueDate")] BorrowRecord borrowRecord)
        {
            var item = await _context.Items.FindAsync(borrowRecord.ItemId);

            // Server-side guard: even if the dropdown was tampered with (or the
            // item's status changed between page load and submit), block it here.
            if (item == null || item.Status != "Available")
            {
                ModelState.AddModelError(string.Empty, "That item is no longer available to borrow.");
            }

            if (ModelState.IsValid)
            {
                borrowRecord.BorrowDate = DateTime.Now;
                _context.Add(borrowRecord);

                item!.Status = "Borrowed";
                _context.Update(item);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available"), "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        /// 
        /// GET: BorrowRecord/Edit/5 - shows the edit form for correcting a
        /// loan's dates directly through the app instead of editing the
        /// database by hand (useful for fixing data-entry mistakes or
        /// backdating for testing purposes).
        /// 
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var borrowRecord = await _context.BorrowRecords.FindAsync(id);
            if (borrowRecord == null)
            {
                return NotFound();
            }

            // Include the currently-assigned item even though it's not "Available"
            // (it's out on loan), otherwise the dropdown wouldn't show it at all.
            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available" || i.Id == borrowRecord.ItemId),
                "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);

            return View(borrowRecord);
        }

        /// POST: BorrowRecord/Edit/5 - saves changes to a loan record, guarding against concurrent edits.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ItemId,BorrowerId,BorrowDate,DueDate,ReturnDate,FineAmount")] BorrowRecord borrowRecord)
        {
            if (id != borrowRecord.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(borrowRecord);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else deleted this record between the page loading and the save.
                    if (!_context.BorrowRecords.Any(r => r.Id == borrowRecord.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available" || i.Id == borrowRecord.ItemId),
                "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        /// 
        /// POST: BorrowRecord/Return/5 - marks a loan as returned, calculates
        /// a late fine if applicable, and puts the item back to "Available".
        /// Guarded so an already-returned record can't be "returned" twice.
        /// 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnItem(int id)
        {
            var record = await _context.BorrowRecords
                .Include(b => b.Item)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record != null && record.ReturnDate == null)
            {
                record.ReturnDate = DateTime.Now;

                // Calculate fine ($1 per day overdue)
                if (record.ReturnDate > record.DueDate)
                {
                    var daysOverdue = (record.ReturnDate.Value - record.DueDate).Days;
                    record.FineAmount = daysOverdue * DailyFineRate;
                }

                // Update item status back to Available
                if (record.Item != null)
                {
                    record.Item.Status = "Available";
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
