using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

/// <summary>Member self-service area. All queries are scoped to the signed-in member.</summary>
[Authorize(Roles = "Member")]
[Route("Me")]
public sealed class MeController : Controller
{
    private readonly LibraryDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MeController(LibraryDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = _users.GetUserId(User);
        var user = await _users.GetUserAsync(User);
        if (userId == null || user == null) return Challenge();

        var borrower = await _db.Borrowers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.ApplicationUserId == userId);
        if (borrower == null)
            return View("NotLinked");

        var activeLoans = await _db.BorrowRecords.AsNoTracking()
            .Include(r => r.Item).ThenInclude(i => i!.Branch)
            .Where(r => r.BorrowerId == borrower.Id && r.ReturnDate == null)
            .OrderBy(r => r.DueDate).ToListAsync();

        var reservations = await _db.Reservations.AsNoTracking()
            .Include(r => r.Item)
            .Where(r => r.BorrowerId == borrower.Id &&
                (r.Status == "Waiting" || r.Status == "Ready"))
            .OrderBy(r => r.RequestedDate).ToListAsync();

        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.Recipient == borrower.Email || n.Recipient == user.Email)
            .OrderByDescending(n => n.SentAt).Take(8).ToListAsync();

        var allFines = await _db.BorrowRecords.AsNoTracking()
            .Where(r => r.BorrowerId == borrower.Id && r.FineAmount > 0)
            .SumAsync(r => (decimal?)r.FineAmount) ?? 0m;
        var overdue = activeLoans.Where(r => r.DueDate < DateTime.Now).ToList();
        var outstanding = overdue.Sum(r => Math.Max(0, (DateTime.Now.Date - r.DueDate.Date).Days) * BorrowRecordController.DailyFineRate);

        return View(new MeDashboardViewModel
        {
            Borrower = borrower,
            ActiveLoans = activeLoans,
            Reservations = reservations,
            Notifications = notifications,
            RecordedFines = allFines,
            EstimatedOutstandingFines = outstanding,
            OverdueLoanCount = overdue.Count
        });
    }
}
