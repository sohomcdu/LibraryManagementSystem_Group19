using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using System.Data;
namespace LibraryManagementSystem.Controllers
{
    public class TransferController : Controller
    {
        private readonly LibraryDbContext _context;
        public TransferController(LibraryDbContext context) => _context = context;

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var requests = await _context.TransferRequests
                .Include(r => r.FromBranch)
                .Include(r => r.ToBranch)
                .Include(r => r.RequestedItems)
                    .ThenInclude(i => i.Item)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
            var transfers = await _context.ItemTransfers
                .Include(t => t.Item)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .OrderByDescending(t => t.InitiatedDate)
                .ToListAsync();
            return View(new TransferIndexViewModel { Requests = requests, Transfers = transfers });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(string? itemCategory = null, int? fromBranchId = null, int? toBranchId = null)
        {
            var model = new TransferRequestFormViewModel
            {
                ItemCategory = itemCategory ?? string.Empty,
                FromBranchId = fromBranchId ?? 0,
                ToBranchId = toBranchId ?? 0
            };
            await PopulateCreateViewAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(TransferRequestFormViewModel model)
        {
            model.SelectedItemIds = model.SelectedItemIds.Distinct().ToList();

            if (model.ItemCategory is not ("Book" or "Music" or "Toy"))
            {
                ModelState.AddModelError(nameof(model.ItemCategory), "Choose a valid item category.");
            }
            if (model.SelectedItemIds.Count is < 1 or > 1000)
            {
                ModelState.AddModelError(nameof(model.SelectedItemIds), "Select between 1 and 1,000 available copies.");
            }

            var activeBranchIds = await _context.Branches
                .Where(b => b.IsActive && (b.Id == model.FromBranchId || b.Id == model.ToBranchId))
                .Select(b => b.Id)
                .ToListAsync();
            if (model.FromBranchId == model.ToBranchId || activeBranchIds.Count != 2)
            {
                ModelState.AddModelError(string.Empty, "Choose two different active branches.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateViewAsync(model);
                return View(model);
            }

            var selectedItems = await _context.Items
                .Where(i => model.SelectedItemIds.Contains(i.Id)
                    && i.Status == "Available"
                    && i.BranchId == model.FromBranchId)
                .ToListAsync();
            var matchingCategoryItems = model.ItemCategory switch
            {
                "Book" => selectedItems.OfType<Book>().Cast<Item>().ToList(),
                "Music" => selectedItems.OfType<Music>().Cast<Item>().ToList(),
                "Toy" => selectedItems.OfType<Toy>().Cast<Item>().ToList(),
                _ => []
            };
            if (matchingCategoryItems.Count != model.SelectedItemIds.Count)
            {
                ModelState.AddModelError(string.Empty, "One or more selected copies are no longer available at the source branch. Refresh the inventory list and try again.");
                await PopulateCreateViewAsync(model);
                return View(model);
            }

            var request = new TransferRequest
            {
                ItemCategory = model.ItemCategory,
                FromBranchId = model.FromBranchId,
                ToBranchId = model.ToBranchId,
                Quantity = matchingCategoryItems.Count,
                RequestedBy = User.Identity?.Name ?? "Admin",
                RequestedAt = DateTime.Now,
                Status = "Pending"
            };
            foreach (var item in matchingCategoryItems)
            {
                request.RequestedItems.Add(new TransferRequestItem { ItemId = item.Id });
            }
            _context.TransferRequests.Add(request);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Transfer request sent to the Manager for stock review.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        // POST: Transfer/Receive/5 — the destination branch confirms the item has arrived.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id)
        {
            var transfer = await _context.ItemTransfers
                .Include(t => t.Item)
                .Include(t => t.ToBranch)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null || transfer.Status != "InTransit") return NotFound();
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

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Requests()
        {
            var requests = await _context.TransferRequests
                .Include(r => r.FromBranch)
                .Include(r => r.ToBranch)
                .Include(r => r.RequestedItems)
                    .ThenInclude(i => i.Item)
                .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                .ThenByDescending(r => r.RequestedAt)
                .ToListAsync();
            var rows = new List<TransferRequestReviewRow>();
            foreach (var request in requests)
            {
                var selectedAvailable = request.RequestedItems.Count == 0
                    ? await AvailableStockAsync(request.ItemCategory, request.FromBranchId)
                    : request.RequestedItems.Count(i => i.Item?.Status == "Available" && i.Item.BranchId == request.FromBranchId);
                rows.Add(new TransferRequestReviewRow
                {
                    Request = request,
                    SelectedAvailable = selectedAvailable
                });
            }
            return View(rows);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var request = await _context.TransferRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["Error"] = "This transfer request has already been reviewed.";
                return RedirectToAction(nameof(Requests));
            }

            var branchesExist = await _context.Branches
                .Where(b => b.IsActive && (b.Id == request.FromBranchId || b.Id == request.ToBranchId))
                .CountAsync() == 2;
            if (!branchesExist)
            {
                TempData["Error"] = "The source or destination branch is no longer active.";
                return RedirectToAction(nameof(Requests));
            }

            var requestedItemIds = await _context.TransferRequestItems
                .Where(i => i.TransferRequestId == request.Id)
                .Select(i => i.ItemId)
                .ToListAsync();
            var items = requestedItemIds.Count == 0
                ? await AvailableItems(request.ItemCategory, request.FromBranchId)
                    .OrderBy(i => i.Id)
                    .Take(request.Quantity)
                    .ToListAsync()
                : await AvailableItems(request.ItemCategory, request.FromBranchId)
                    .Where(i => requestedItemIds.Contains(i.Id))
                    .ToListAsync();
            if (items.Count < request.Quantity)
            {
                TempData["Error"] = $"Insufficient available stock. This request needs {request.Quantity}, but only {items.Count} are available.";
                return RedirectToAction(nameof(Requests));
            }

            var now = DateTime.Now;
            foreach (var item in items)
            {
                item.Status = "InTransit";
                _context.ItemTransfers.Add(new ItemTransfer
                {
                    ItemId = item.Id,
                    FromBranchId = request.FromBranchId,
                    ToBranchId = request.ToBranchId,
                    TransferRequestId = request.Id,
                    InitiatedDate = now,
                    Status = "InTransit"
                });
            }
            request.Status = "Approved";
            request.ReviewedBy = User.Identity?.Name ?? "Manager";
            request.ReviewedAt = now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = $"Request approved. {items.Count} items are now in transit.";
            return RedirectToAction(nameof(Requests));
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.TransferRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["Error"] = "This transfer request has already been reviewed.";
                return RedirectToAction(nameof(Requests));
            }

            request.Status = "Rejected";
            request.ReviewedBy = User.Identity?.Name ?? "Manager";
            request.ReviewedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Transfer request rejected.";
            return RedirectToAction(nameof(Requests));
        }

        private async Task PopulateCreateViewAsync(TransferRequestFormViewModel model)
        {
            var branches = await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            ViewData["FromBranchId"] = new SelectList(branches, "Id", "Name", model.FromBranchId);
            ViewData["ToBranchId"] = new SelectList(branches, "Id", "Name", model.ToBranchId);
            if (model.FromBranchId > 0 && model.ItemCategory is "Book" or "Music" or "Toy")
            {
                model.AvailableItems = await AvailableItems(model.ItemCategory, model.FromBranchId)
                    .OrderBy(i => i.Name)
                    .ThenBy(i => i.LibraryCode)
                    .ToListAsync();
            }
        }

        private IQueryable<Item> AvailableItems(string category, int branchId)
        {
            IQueryable<Item> query = category switch
            {
                "Book" => _context.Books,
                "Music" => _context.Music,
                "Toy" => _context.Toys,
                _ => _context.Items.Where(i => false)
            };
            return query.Where(i => i.Status == "Available" && i.BranchId == branchId);
        }

        private Task<int> AvailableStockAsync(string category, int branchId) =>
            AvailableItems(category, branchId).CountAsync();
    }
}
