using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    public class BorrowRecordController : Controller
    {
        public const decimal DailyFineRate = 1.00m;

        private readonly LibraryDbContext _context;
        public BorrowRecordController(LibraryDbContext context) => _context = context;

        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Index()
        {
            var query = _context.BorrowRecords.Include(b => b.Item).Include(b => b.Borrower).AsQueryable();
            if (User.IsInRole("Reception"))
            {
                var branchCode = CurrentBranchCode();
                query = query.Where(r => r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
            }
            var records = await query.OrderByDescending(r => r.BorrowDate).ToListAsync();
            return View(records);
        }

        [Authorize(Roles = "Admin,Reception")]
        public IActionResult Create() => View(new BorrowRecord());

        [HttpGet]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> AvailableItems(string category)
        {
            IQueryable<Item> query = category switch
            {
                "Book" => _context.Books,
                "Music" => _context.Music,
                "Toy" => _context.Toys,
                _ => _context.Items.Where(i => false)
            };
            var items = await AvailableItemsForCurrentUser(query)
                .OrderBy(i => i.Name)
                .ThenBy(i => i.LibraryCode)
                .ToListAsync();
            return Json(items.Select(item => new
            {
                item.Id,
                item.Name,
                item.LibraryCode,
                CategoryDetails = item switch
                {
                    Book book => $"{book.Author} · {book.Genre}",
                    Music music => $"{music.Artist} · {music.ReleaseYear}",
                    Toy toy => $"{toy.Type} · age {toy.MinimumAge}+",
                    _ => string.Empty
                }
            }));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> BorrowerSuggestions(string term)
        {
            var prefix = term?.Trim();
            if (string.IsNullOrWhiteSpace(prefix))
                return Json(Array.Empty<object>());

            var borrowers = await _context.Borrowers
                .AsNoTracking()
                .Where(b => b.FullName.StartsWith(prefix))
                .OrderBy(b => b.FullName)
                .Take(12)
                .Select(b => new { b.Id, b.FullName, b.Email })
                .ToListAsync();
            return Json(borrowers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Create([Bind("ItemId,BorrowerId,DueDate")] BorrowRecord borrowRecord)
        {
            var item = await _context.Items.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == borrowRecord.ItemId);
            if (item == null || item.Status != "Available" || !CanServeItemAtCurrentBranch(item))
            {
                ModelState.AddModelError(string.Empty, "That item is no longer available to borrow at this branch.");
            }
            if (!await _context.Borrowers.AnyAsync(b => b.Id == borrowRecord.BorrowerId))
            {
                ModelState.AddModelError(nameof(borrowRecord.BorrowerId), "Select a valid borrower from the matching suggestions.");
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

            return View(borrowRecord);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var borrowRecord = await _context.BorrowRecords.Include(r => r.Item).ThenInclude(i => i!.Branch).FirstOrDefaultAsync(r => r.Id == id);
            if (borrowRecord == null) return NotFound();

            // A loan's item cannot be changed in-place; return it and create a new loan instead.
            var editableItems = _context.Items.Where(i => i.Id == borrowRecord.ItemId);
            ViewData["ItemId"] = new SelectList(editableItems, "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ItemId,BorrowerId,BorrowDate,DueDate,ReturnDate,FineAmount")] BorrowRecord borrowRecord)
        {
            if (id != borrowRecord.Id) return NotFound();
            var existingRecord = await _context.BorrowRecords.Include(r => r.Item).ThenInclude(i => i!.Branch).FirstOrDefaultAsync(r => r.Id == id);
            if (existingRecord == null) return NotFound();
            if (borrowRecord.ItemId != existingRecord.ItemId)
                return BadRequest("The item on an existing loan cannot be changed. Return this loan and create a new record instead.");

            if (ModelState.IsValid)
            {
                try
                {
                    existingRecord.ItemId = borrowRecord.ItemId;
                    existingRecord.BorrowerId = borrowRecord.BorrowerId;
                    existingRecord.BorrowDate = borrowRecord.BorrowDate;
                    existingRecord.DueDate = borrowRecord.DueDate;
                    existingRecord.ReturnDate = borrowRecord.ReturnDate;
                    existingRecord.FineAmount = borrowRecord.FineAmount;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.BorrowRecords.Any(r => r.Id == borrowRecord.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var editableItems = _context.Items.Where(i => i.Id == borrowRecord.ItemId);
            ViewData["ItemId"] = new SelectList(editableItems, "Id", "Name", borrowRecord.ItemId);
            ViewData["BorrowerId"] = new SelectList(_context.Borrowers, "Id", "FullName", borrowRecord.BorrowerId);
            return View(borrowRecord);
        }

        // POST: BorrowRecord/Return/5 — now checks the F5 waitlist instead of
        // always setting the item straight back to "Available".
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> ReturnItem(int id)
        {
            var record = await _context.BorrowRecords
                .Include(b => b.Item).ThenInclude(i => i!.Branch)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();
            if (!CanServeItemAtCurrentBranch(record.Item))
                return Forbid();
            if (record.ReturnDate == null)
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

        private IQueryable<Item> AvailableItemsForCurrentUser(IQueryable<Item>? source = null)
        {
            var items = (source ?? _context.Items).Where(i => i.Status == "Available");
            if (User.IsInRole("Reception"))
            {
                var branchCode = CurrentBranchCode();
                items = items.Where(i => i.Branch != null && i.Branch.Code == branchCode);
            }
            return items;
        }

        private bool CanServeItemAtCurrentBranch(Item? item) =>
            !User.IsInRole("Reception") || item?.Branch?.Code == CurrentBranchCode();

        private string CurrentBranchCode() => Request.Cookies["LMS.BranchCode"] ?? "CEN";
    }
}