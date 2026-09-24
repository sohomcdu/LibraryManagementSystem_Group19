using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;

namespace LibraryManagementSystem.Controllers
{
    /// 
    /// Read-only statistics dashboard for the Manager role. Covers the three
    /// categories the assignment brief requires: item status stats,
    /// borrowing stats, and fine stats. Deliberately has no CRUD actions -
    /// a Manager views data here, they don't edit it.
    /// 
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        /// EF Core database context, injected via dependency injection.
        private readonly LibraryDbContext _context;

        /// Creates the controller with its required database context.
        public ManagerController(LibraryDbContext context)
        {
            _context = context;
        }

        /// 
        /// GET: Manager - runs every statistic query and assembles them into
        /// a single view model for the dashboard.
        /// 
        public async Task<IActionResult> Index()
        {
            // Item status breakdown (available/borrowed/damaged/destroy)
            var itemStatusCounts = await _context.Items
                .GroupBy(i => i.Status)
                .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalItems = itemStatusCounts.Sum(s => s.Count);

            // Borrowing stats
            var totalBorrows = await _context.BorrowRecords.CountAsync();
            var activeBorrows = await _context.BorrowRecords.CountAsync(r => r.ReturnDate == null);
            var overdueBorrows = await _context.BorrowRecords
                .CountAsync(r => r.ReturnDate == null && r.DueDate < DateTime.Now);
            var returnedOnTime = await _context.BorrowRecords
                .CountAsync(r => r.ReturnDate != null && r.ReturnDate <= r.DueDate);

            // Fine stats
            var totalFinesIssued = await _context.BorrowRecords
                .Where(r => r.FineAmount > 0)
                .SumAsync(r => r.FineAmount);
            var collectedFines = await _context.BorrowRecords
                .Where(r => r.FineAmount > 0 && r.ReturnDate != null)
                .SumAsync(r => r.FineAmount);

            // Projected fines: overdue items not yet returned don't have FineAmount set
            // yet (that only happens on return), so this estimates what's currently
            // accruing using the same $1/day rate as ReturnItem.
            var overdueActiveRecords = await _context.BorrowRecords
                .Where(r => r.ReturnDate == null && r.DueDate < DateTime.Now)
                .ToListAsync();

            var outstandingFines = overdueActiveRecords
                .Sum(r => (decimal)(DateTime.Now - r.DueDate).Days * BorrowRecordController.DailyFineRate);

            var viewModel = new ManagerDashboardViewModel
            {
                ItemStatusCounts = itemStatusCounts,
                TotalItems = totalItems,
                TotalBorrows = totalBorrows,
                ActiveBorrows = activeBorrows,
                OverdueBorrows = overdueBorrows,
                ReturnedOnTime = returnedOnTime,
                TotalFinesIssued = totalFinesIssued,
                OutstandingFines = outstandingFines,
                CollectedFines = collectedFines
            };

            return View(viewModel);
        }
    }

    /// Simple item-status-to-count pairing, used to render the item status breakdown on the dashboard.
    public class StatusCount
    {
        /// The status value being counted (e.g. "Available").
        public string Status { get; set; } = string.Empty;

        /// How many items currently have this status.
        public int Count { get; set; }
    }

    /// Aggregates every statistic ManagerController.Index calculates, so the view has a single strongly-typed model to bind to.
    public class ManagerDashboardViewModel
    {
        /// Item counts grouped by status.
        public List<StatusCount> ItemStatusCounts { get; set; } = new();

        /// Total number of items in the catalogue, across all statuses.
        public int TotalItems { get; set; }

        /// Total loans ever created, active and completed combined.
        public int TotalBorrows { get; set; }

        /// Loans currently out (not yet returned).
        public int ActiveBorrows { get; set; }

        /// Loans currently out and past their due date.
        public int OverdueBorrows { get; set; }

        /// Loans that were returned on or before their due date.
        public int ReturnedOnTime { get; set; }

        /// Sum of every fine ever charged on a completed return.
        public decimal TotalFinesIssued { get; set; }

        /// Estimated fine total currently accruing on active overdue loans (not yet charged, since fines are only finalized on return).
        public decimal OutstandingFines { get; set; }

        /// Sum of fines actually charged and finalized on completed returns.
        public decimal CollectedFines { get; set; }
    }
}
