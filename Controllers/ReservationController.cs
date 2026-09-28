using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReservationController(LibraryDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Reservation ("My Reservations" for a Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return NotFound("No borrower profile linked to this account.");

            var reservations = await _context.Reservations
                .Include(r => r.Item)
                .Where(r => r.BorrowerId == borrower.Id)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return View(reservations);
        }

        // POST: Reservation/Create — only allowed while the item is Borrowed or Damaged (per spec).
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int itemId)
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return NotFound("No borrower profile linked to this account.");

            var item = await _context.Items.FindAsync(itemId);
            if (item == null || (item.Status != "Borrowed" && item.Status != "Damaged"))
            {
                TempData["Error"] = "You can only join the waitlist for an item that is currently borrowed or damaged.";
                return RedirectToAction("Index", "Search");
            }

            bool alreadyWaiting = await _context.Reservations.AnyAsync(r =>
                r.ItemId == itemId && r.BorrowerId == borrower.Id && r.Status == "Waiting");
            if (alreadyWaiting)
            {
                TempData["Error"] = "You're already on the waitlist for this item.";
                return RedirectToAction("Index", "Search");
            }

            _context.Reservations.Add(new Reservation
            {
                ItemId = itemId,
                BorrowerId = borrower.Id,
                RequestedDate = DateTime.Now,
                Status = "Waiting"
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Reservation/Cancel/5 — a Member withdraws their own waiting reservation.
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var borrower = await GetCurrentBorrowerAsync();
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null || borrower == null || reservation.BorrowerId != borrower.Id)
                return NotFound();

            if (reservation.Status == "Waiting")
            {
                reservation.Status = "Cancelled";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Reservation/Holds — Reception/Admin queue management view.
        [Authorize(Roles = "Reception,Admin")]
        public async Task<IActionResult> Holds()
        {
            // Expire any "Ready" hold whose pickup window has passed, and
            // promote the next person in that item's queue if there is one.
            var expired = await _context.Reservations
                .Include(r => r.Item)
                .Where(r => r.Status == "Ready" && r.PickupExpiresAt != null && r.PickupExpiresAt < DateTime.Now)
                .ToListAsync();

            foreach (var res in expired)
            {
                res.Status = "Expired";
                if (res.Item != null)
                {
                    await PromoteNextOrMakeAvailableAsync(res.Item);
                }
            }
            if (expired.Any()) await _context.SaveChangesAsync();

            var holds = await _context.Reservations
                .Include(r => r.Item)
                .Include(r => r.Borrower)
                .Where(r => r.Status == "Waiting" || r.Status == "Ready")
                .OrderBy(r => r.RequestedDate)
                .ToListAsync();

            return View(holds);
        }

        // POST: Reservation/Fulfil/5 — Reception hands the held item to the patron,
        // converting the reservation into a real BorrowRecord.
        [HttpPost]
        [Authorize(Roles = "Reception,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfil(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null || reservation.Status != "Ready" || reservation.Item == null)
                return NotFound();

            _context.BorrowRecords.Add(new BorrowRecord
            {
                ItemId = reservation.ItemId,
                BorrowerId = reservation.BorrowerId,
                BorrowDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(14)
            });

            reservation.Item.Status = "Borrowed";
            reservation.Status = "Fulfilled";

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Holds));
        }

        /// <summary>
        /// Shared logic called from BorrowRecordController.ReturnItem and from
        /// hold-expiry above: when an item frees up, either promote the next
        /// waiting reservation (item goes to "Reserved") or, if the queue is
        /// empty, put the item back to "Available".
        /// </summary>
        public static async Task PromoteNextOrMakeAvailableAsync(LibraryDbContext context, Item item)
        {
            var next = await context.Reservations
                .Where(r => r.ItemId == item.Id && r.Status == "Waiting")
                .OrderBy(r => r.RequestedDate)
                .FirstOrDefaultAsync();

            if (next != null)
            {
                next.Status = "Ready";
                next.PickupExpiresAt = DateTime.Now.AddDays(3); // matches the spec's "Collect by {expires}"
                item.Status = "Reserved";
                await NotificationService.LogHoldAvailableAsync(context, next);
            }
            else
            {
                item.Status = "Available";
            }
        }

        // Instance wrapper so Holds() above can call it without changing its own DbContext reference style.
        private async Task PromoteNextOrMakeAvailableAsync(Item item) =>
            await PromoteNextOrMakeAvailableAsync(_context, item);

        private async Task<Borrower?> GetCurrentBorrowerAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Borrowers.FirstOrDefaultAsync(b => b.ApplicationUserId == userId);
        }
    }
}