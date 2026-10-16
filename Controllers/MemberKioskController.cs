using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Self-service kiosk for signed-in members. Works like the in-library kiosk, but the member is
/// identified by their own login instead of typing an email: they pick the branch they are at,
/// scan or type an item's library code, and the loan is created straight away (14 days).
/// Only items held at the chosen branch and currently Available can be checked out.
/// </summary>
[Authorize(Roles = "Member")]
[Route("Me/Kiosk")]
public sealed class MemberKioskController : Controller
{
    private const string BranchCookie = "LMS.MemberKioskBranch";
    private readonly LibraryDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MemberKioskController(LibraryDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var borrower = await CurrentBorrowerAsync();
        if (borrower == null) return View("~/Views/Me/NotLinked.cshtml");
        var vm = await BuildAsync(borrower, Request.Cookies[BranchCookie]);
        return View(vm);
    }

    [HttpPost("checkout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string branchCode, string itemCode)
    {
        var borrower = await CurrentBorrowerAsync();
        if (borrower == null) return View("~/Views/Me/NotLinked.cshtml");

        branchCode = (branchCode ?? string.Empty).Trim().ToUpperInvariant();
        var code = (itemCode ?? string.Empty).Trim().ToUpperInvariant();
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Code == branchCode && b.IsActive);

        string? error = null, message = null;
        if (branch == null) error = "Choose the library branch you are at.";
        else if (code.Length == 0) error = "Scan or type the item's library code (for example BK-1001).";
        else
        {
            Response.Cookies.Append(BranchCookie, branch.Code, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
            var item = await _db.Items.Include(i => i.Branch).FirstOrDefaultAsync(i => i.LibraryCode == code);
            if (item == null) error = "Item code not found. Check the label or ask a library staff member.";
            else if (item.Branch == null || item.Branch.Code != branch.Code)
                error = $"{item.Name} belongs to {item.Branch?.Name ?? "another branch"}. You can only check out items held at {branch.Name}.";
            else if (item.Status != "Available")
                error = $"{item.Name} is currently {item.Status.ToLowerInvariant()} and cannot be checked out.";
            else
            {
                var record = new BorrowRecord { ItemId = item.Id, BorrowerId = borrower.Id, BorrowDate = DateTime.Now, DueDate = DateTime.Now.AddDays(14) };
                item.Status = "Borrowed";
                _db.BorrowRecords.Add(record);
                await _db.SaveChangesAsync();
                await NotificationService.LogBorrowReceiptAsync(_db, record, item);
                message = $"Checkout complete. {item.Name} is due {record.DueDate:dd MMM yyyy}. A receipt has been added to your notifications.";
                code = string.Empty;
            }
        }

        var vm = await BuildAsync(borrower, branch?.Code ?? branchCode);
        vm.ItemCode = code;
        vm.Error = error;
        vm.Message = message;
        return View("Index", vm);
    }

    private async Task<Borrower?> CurrentBorrowerAsync()
    {
        var userId = _users.GetUserId(User);
        if (userId == null) return null;
        return await _db.Borrowers.FirstOrDefaultAsync(b => b.ApplicationUserId == userId);
    }

    private async Task<MemberKioskViewModel> BuildAsync(Borrower borrower, string? branchCode)
    {
        var branches = await _db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
        return new MemberKioskViewModel
        {
            Borrower = borrower,
            Branches = branches,
            BranchCode = branches.Any(b => b.Code == branchCode) ? branchCode : branches.FirstOrDefault()?.Code,
            ActiveLoans = await _db.BorrowRecords.AsNoTracking().Include(r => r.Item).ThenInclude(i => i!.Branch)
                .Where(r => r.BorrowerId == borrower.Id && r.ReturnDate == null)
                .OrderBy(r => r.DueDate).ToListAsync()
        };
    }
}
