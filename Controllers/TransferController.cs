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
    public class TransferController : Controller
    {
        private readonly LibraryDbContext _context;
        public TransferController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var transfers = await _context.ItemTransfers
                .Include(t => t.Item)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .OrderByDescending(t => t.InitiatedDate)
                .ToListAsync();
            return View(transfers);
        }

        public IActionResult Create()
        {
            // Only items already sitting Available at some branch can be transferred.
            ViewData["ItemId"] = new SelectList(
                _context.Items.Where(i => i.Status == "Available" && i.BranchId != null), "Id", "Name");
            ViewData["ToBranchId"] = new SelectList(_context.Branches.Where(b => b.IsActive), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int itemId, int toBranchId)
        {
            var item = await _context.Items.FindAsync(itemId);
            if (item == null || item.Status != "Available" || item.BranchId == null)
            {
                ModelState.AddModelError(string.Empty, "That item can no longer be transferred.");
                ViewData["ItemId"] = new SelectList(_context.Items.Where(i => i.Status == "Available" && i.BranchId != null), "Id", "Name", itemId);
                ViewData["ToBranchId"] = new SelectList(_context.Branches.Where(b => b.IsActive), "Id", "Name", toBranchId);
                return View();
            }

            var transfer = new ItemTransfer
            {
                ItemId = itemId,
                FromBranchId = item.BranchId.Value,
                ToBranchId = toBranchId,
                InitiatedDate = DateTime.Now,
                Status = "InTransit"
            };
            _context.ItemTransfers.Add(transfer);

            // Item leaves circulation while it's physically moving.
            item.Status = "InTransit";
            _context.Update(item);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfer/Receive/5 — the destination branch confirms the item has arrived.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id)
        {
            var transfer = await _context.ItemTransfers
                .Include(t => t.Item)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null || transfer.Status != "InTransit")
            {
                return NotFound();
            }

            transfer.Status = "Completed";
            transfer.ReceivedDate = DateTime.Now;

            if (transfer.Item != null)
            {
                transfer.Item.BranchId = transfer.ToBranchId;
                transfer.Item.Status = "Available";
            }

            await _context.SaveChangesAsync();

            // F3 hook: log a "Transfer arrived" staff notification (see NotificationService).
            await NotificationService.LogTransferArrivedAsync(_context, transfer);

            return RedirectToAction(nameof(Index));
        }
    }
}
