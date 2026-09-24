using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Handles the public request-to-borrow workflow for Members. A Member
    /// can only ask for an item; Reception always makes the final call by
    /// approving or rejecting the request. Approving creates a real
    /// BorrowRecord, reusing the same effect as BorrowRecordController.Create.
    /// The base [Authorize] with no role requires any signed-in user, while
    /// individual actions narrow that further to Member or Reception.
    /// 
    [Authorize]
    public class BorrowRequestController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Used to resolve the currently signed-in user's Id, so we can find their linked Borrower profile.
        private readonly UserManager<ApplicationUser> _userManager;

        /// Creates the controller with its required database context and Identity user manager.
        public BorrowRequestController(LibraryDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// GET: BorrowRequest ("My Requests") - lists the signed-in Member's own request history, newest first.
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null)
            {
                return NotFound("No borrower profile linked to this account.");
            }

            var requests = await _context.BorrowRequests
                .Include(r => r.Item)
                .Where(r => r.BorrowerId == borrower.Id)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        /// GET: BorrowRequest/Create - shows the request form, with the item dropdown limited to available items and optionally pre-selected via itemId (used when linking here from the Search page).
        [Authorize(Roles = "Member")]
        public IActionResult Create(int? itemId)
        {
            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available"), "Id", "Name", itemId);
            return View();
        }

        /// POST: BorrowRequest/Create - creates a new Pending request for the signed-in Member, after re-checking the item is actually still available.
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int itemId)
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null)
            {
                return NotFound("No borrower profile linked to this account.");
            }

            var item = await _context.Items.FindAsync(itemId);
            if (item == null || item.Status != "Available")
            {
                ModelState.AddModelError(string.Empty, "That item is no longer available.");
                ViewData["ItemId"] = new SelectList(
                    _context.Items.Where(i => i.Status == "Available"), "Id", "Name", itemId);
                return View();
            }

            _context.BorrowRequests.Add(new BorrowRequest
            {
                ItemId = itemId,
                BorrowerId = borrower.Id,
                RequestDate = DateTime.Now,
                Status = "Pending"
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// GET: BorrowRequest/Pending - Reception's queue of every request still awaiting a decision, oldest first.
        [Authorize(Roles = "Reception")]
        public async Task<IActionResult> Pending()
        {
            var requests = await _context.BorrowRequests
                .Include(r => r.Item)
                .Include(r => r.Borrower)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        /// 
        /// POST: BorrowRequest/Approve/5 - turns a Pending request into a real
        /// loan: creates a BorrowRecord, flips the item's status to
        /// "Borrowed", and marks the request as Approved, linking the two
        /// records together via BorrowRecordId.
        /// 
        [HttpPost]
        [Authorize(Roles = "Reception")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.BorrowRequests
                .Include(r => r.Item)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null || request.Status != "Pending")
            {
                return NotFound();
            }

            if (request.Item == null || request.Item.Status != "Available")
            {
                ModelState.AddModelError(string.Empty, "Item is no longer available.");
                return RedirectToAction(nameof(Pending));
            }

            var borrowRecord = new BorrowRecord
            {
                ItemId = request.ItemId,
                BorrowerId = request.BorrowerId,
                BorrowDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(14)
            };
            _context.BorrowRecords.Add(borrowRecord);

            request.Item.Status = "Borrowed";
            request.Status = "Approved";

            await _context.SaveChangesAsync();

            // Second save: BorrowRecord.Id is only assigned by the database
            // after the first SaveChangesAsync commits, so BorrowRecordId can
            // only be set on the request afterward.
            request.BorrowRecordId = borrowRecord.Id;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Pending));
        }

        /// POST: BorrowRequest/Reject/5 - marks a Pending request as Rejected without creating any loan.
        [HttpPost]
        [Authorize(Roles = "Reception")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.BorrowRequests.FindAsync(id);
            if (request == null || request.Status != "Pending")
            {
                return NotFound();
            }

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Pending));
        }

        /// GET: BorrowRequest/ForBorrower/5 - shows one specific borrower's full request history (any status), so Reception can look up "what has this person asked for" from their profile.
        [Authorize(Roles = "Reception")]
        public async Task<IActionResult> ForBorrower(int borrowerId)
        {
            var borrower = await _context.Borrowers.FindAsync(borrowerId);
            if (borrower == null)
            {
                return NotFound();
            }

            var requests = await _context.BorrowRequests
                .Include(r => r.Item)
                .Where(r => r.BorrowerId == borrowerId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            ViewData["BorrowerName"] = borrower.FullName;
            return View(requests);
        }

        /// Looks up the Borrower profile linked to the currently signed-in user's account, if one exists.
        private async Task<Borrower?> GetCurrentBorrowerAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Borrowers.FirstOrDefaultAsync(b => b.ApplicationUserId == userId);
        }
    }
}
