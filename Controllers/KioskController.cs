using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

[Route("Kiosk")]
[Authorize(Roles = "Kiosk")]
public sealed class KioskController : Controller
{
    private readonly LibraryDbContext _db;
    private readonly SignInManager<ApplicationUser> _signInManager;
    public KioskController(LibraryDbContext db, SignInManager<ApplicationUser> signInManager)
    { _db = db; _signInManager = signInManager; }

    [AllowAnonymous, HttpGet("Activate")]
    public IActionResult Activate()
    {
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin") && !User.IsInRole("Reception")) return Forbid();
        return View(new KioskActivationViewModel());
    }

    [AllowAnonymous, HttpPost("Activate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(KioskActivationViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin") && !User.IsInRole("Reception")) return Forbid();
        if (!ModelState.IsValid) return View(model);
        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Kiosk device credentials were not recognised.");
            return View(model);
        }
        // Role is checked against the database after successful sign-in to prevent ordinary accounts entering kiosk mode.
        if (!await IsKioskRoleAsync(model.Email))
        {
            await _signInManager.SignOutAsync();
            ModelState.AddModelError(string.Empty, "This account is not registered as a kiosk device.");
            return View(model);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsKioskRoleAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return false;
        var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.IsInRoleAsync(user, "Kiosk");
    }

    [HttpGet("")]
    public IActionResult Index() => View(new KioskPageViewModel());

    [HttpPost("account"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Account(string cardEmail)
    {
        var vm = new KioskPageViewModel { CardEmail = cardEmail?.Trim() };
        if (string.IsNullOrWhiteSpace(vm.CardEmail)) vm.Error = "Enter the email registered to your library account.";
        else
        {
            vm.Borrower = await _db.Borrowers.FirstOrDefaultAsync(b => b.Email == vm.CardEmail);
            if (vm.Borrower == null) vm.Error = "No library account matched that email. Please ask Reception for help.";
            else vm.ActiveLoans = await _db.BorrowRecords.Include(r => r.Item).Where(r => r.BorrowerId == vm.Borrower.Id && r.ReturnDate == null).OrderBy(r => r.DueDate).ToListAsync();
        }
        return View("Index", vm);
    }

    [HttpPost("checkout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string cardEmail, string itemCode)
    {
        var vm = new KioskPageViewModel { CardEmail = cardEmail?.Trim(), ItemCode = itemCode?.Trim() };
        var borrower = await _db.Borrowers.FirstOrDefaultAsync(b => b.Email == vm.CardEmail);
        if (borrower == null) vm.Error = "Enter a valid registered email first.";
        else
        {
            var kioskEmail = User.Identity?.Name ?? string.Empty;
            var kioskBranch = kioskEmail.StartsWith("kiosk.northside@", StringComparison.OrdinalIgnoreCase) ? "NTH"
                : kioskEmail.StartsWith("kiosk.riverside@", StringComparison.OrdinalIgnoreCase) ? "RIV" : "CEN";
            var item = await _db.Items.Include(i => i.Branch).FirstOrDefaultAsync(i => i.LibraryCode == vm.ItemCode);
            vm.ScannedItem = item;
            if (item == null) vm.Error = "Item code not found. Check the label or ask a library staff member.";
            else if (item.Branch == null || item.Branch.Code != kioskBranch) vm.Error = "This item belongs to another branch. Please use the kiosk at its home branch or ask a library staff member about a transfer.";
            else if (item.Status != "Available") vm.Error = $"{item.Name} is currently {item.Status.ToLowerInvariant()} and cannot be checked out.";
            else
            {
                var record = new BorrowRecord { ItemId = item.Id, BorrowerId = borrower.Id, BorrowDate = DateTime.Now, DueDate = DateTime.Now.AddDays(14) };
                item.Status = "Borrowed";
                _db.BorrowRecords.Add(record);
                await _db.SaveChangesAsync();
                await NotificationService.LogBorrowReceiptAsync(_db, record, item);
                vm.Borrower = borrower;
                vm.Message = $"Checkout complete. {item.Name} is due {record.DueDate:dd MMM yyyy}. A simulated receipt has been logged.";
                vm.ActiveLoans = await _db.BorrowRecords.Include(r => r.Item).Where(r => r.BorrowerId == borrower.Id && r.ReturnDate == null).OrderBy(r => r.DueDate).ToListAsync();
            }
        }
        return View("Index", vm);
    }

    [HttpPost("done"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Done()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Activate));
    }
}
