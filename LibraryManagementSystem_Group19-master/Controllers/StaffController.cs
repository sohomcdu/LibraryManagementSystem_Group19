using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

/// <summary>Role-focused workspace for Reception and Admin.</summary>
[Authorize(Roles = "Reception,Admin")]
public sealed class StaffController : Controller
{
    private readonly LibraryDbContext _db;
    public StaffController(LibraryDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin"))
            return View();

        var branchCode = Request.Cookies["LMS.BranchCode"] ?? "CEN";
        ViewBag.CurrentBranch = await _db.Branches.AsNoTracking().Where(b => b.Code == branchCode).Select(b => b.Name).FirstOrDefaultAsync() ?? "All branches";
        ViewBag.BorrowerCount = await _db.Borrowers.CountAsync();
        var pendingQuery = _db.BorrowRequests.Where(r => r.Status == "Pending");
        if (!string.IsNullOrWhiteSpace(branchCode))
            pendingQuery = pendingQuery.Where(r => r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
        ViewBag.PendingRequests = await pendingQuery.CountAsync();
        ViewBag.ActiveLoans = await _db.BorrowRecords.CountAsync(r => r.ReturnDate == null
            && r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
        return View();
    }
}
