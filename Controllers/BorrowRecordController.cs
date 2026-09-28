using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Reception,Admin")]
    public class BorrowRecordController : Controller
    {
        public const decimal DailyFineRate = 1.00m;

        private readonly LibraryDbContext _context;
        public BorrowRecordController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var records = await _context.BorrowRecords
                .Include(b => b.Item)
                .Include(b => b.Borrower)
                .ToListAsync();
            return View(records);
        }

        public IActionResult Create()
        {
            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available"), "Id", "Name");
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ItemId,BorrowerId,DueDate")] BorrowRecord borrowRecord)
        {
            var item = await _context.Items.FindAsync(borrowRecord.ItemId);
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

                // F3 hook: log a borrow-receipt notification.
                await NotificationService.LogBorrowReceiptAsync(_context, borrowRecord, item);

                return RedirectToAction(nameof(Index));
            }

            ViewData["ItemId"] = new SelectList(_context.Items.Where(i => i.Status == "Available"), "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var borrowRecord = await _context.BorrowRecords.FindAsync(id);
            if (borrowRecord == null) return NotFound();

            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available" || i.Id == borrowRecord.ItemId),
                "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ItemId,BorrowerId,BorrowDate,DueDate,ReturnDate,FineAmount")] BorrowRecord borrowRecord)
        {
            if (id != borrowRecord.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(borrowRecord);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.BorrowRecords.Any(r => r.Id == borrowRecord.Id)) return NotFound();
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

        // POST: BorrowRecord/Return/5 — now checks the F5 waitlist instead of
        // always setting the item straight back to "Available".
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

                if (record.ReturnDate > record.DueDate)
                {
                    var daysOverdue = (record.ReturnDate.Value - record.DueDate).Days;
                    record.FineAmount = daysOverdue * DailyFineRate;
                }

                if (record.Item != null)
                {
                    // F5 hook: promotes the next waitlisted patron (sets Status
                    // to "Reserved" and notifies them) or falls back to "Available".
                    await ReservationController.PromoteNextOrMakeAvailableAsync(_context, record.Item);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}