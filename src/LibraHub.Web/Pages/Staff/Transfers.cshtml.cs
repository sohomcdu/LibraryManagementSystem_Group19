using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Mockup 11 – Branch inventory & transfers.</summary>
public class TransfersModel : AppPageModel
{
    public List<(Branch Branch, int Items, int Desks, double AvailPct)> Stats { get; private set; } = new();
    public List<BranchTransfer> Transfers { get; private set; } = new();
    public List<SelectListItem> Branches { get; private set; } = new();
    public BranchTransfer? Highlighted { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Code { get; set; }
    [BindProperty] public int ToBranchId { get; set; }
    [BindProperty] public string Reason { get; set; } = "Patron request";

    public async Task OnGetAsync()
    {
        var branches = await Db.Branches.Include(b => b.Desks).AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        foreach (var b in branches)
        {
            var items = await Db.Items.CountAsync(i => i.CurrentBranchId == b.Id);
            var avail = items == 0 ? 0 : await Db.Items.CountAsync(i => i.CurrentBranchId == b.Id && i.Status == ItemStatus.Available);
            Stats.Add((b, items, b.Desks.Count(d => !d.IsKiosk), items == 0 ? 0 : 100.0 * avail / items));
        }
        Branches = branches.Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToList();

        var branchId = await StaffBranchIdAsync();
        Transfers = await Db.Transfers.Include(t => t.Item).Include(t => t.FromBranch).Include(t => t.ToBranch)
            .Where(t => t.FromBranchId == branchId || t.ToBranchId == branchId)
            .OrderByDescending(t => t.RequestedAt).Take(20).ToListAsync();
        Highlighted = Transfers.FirstOrDefault(t => t.Status == TransferStatus.Dispatched) ?? Transfers.FirstOrDefault();
        ToBranchId = branches.FirstOrDefault(b => b.Id != branchId)?.Id ?? 0;
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var branchId = BranchRestriction;
        Report(await Svc<ItemLifecycleService>().CreateTransferAsync(Code ?? "", ToBranchId, Reason, CurrentUserName, branchId));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDispatchAsync(int id)
    {
        Report(await Svc<ItemLifecycleService>().DispatchAsync(id, CurrentUserName, BranchRestriction));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReceiveAsync(int id, string? scan)
    {
        Report(await Svc<ItemLifecycleService>().ReceiveAsync(id, scan, CurrentUserName, BranchRestriction));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        Report(await Svc<ItemLifecycleService>().CancelTransferAsync(id, CurrentUserName));
        return RedirectToPage();
    }
}
