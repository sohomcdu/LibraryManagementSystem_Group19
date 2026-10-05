using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Reception fines desk – collect / waive against the audit ledger used by Reports › Fine revenue audit.</summary>
public class FinesModel : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "Outstanding";
    public List<Fine> Rows { get; private set; } = new();
    public int OutstandingTotal { get; private set; }

    public async Task OnGetAsync()
    {
        var q = Db.Fines.Include(f => f.Patron).Include(f => f.Loan).ThenInclude(l => l.Item).AsQueryable();
        if (Enum.TryParse<FineStatus>(Status, out var st)) q = q.Where(f => f.Status == st);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var nq = TextUtil.Normalize(Q);
            q = q.Where(f => f.Patron.FullName.ToLower().Contains(nq) || f.Loan.Item.Title.ToLower().Contains(nq));
        }
        Rows = await q.OrderByDescending(f => f.IssuedAt).Take(100).ToListAsync();
        OutstandingTotal = await Db.Fines.Where(f => f.Status == FineStatus.Outstanding).SumAsync(f => f.AmountCents);
    }

    public async Task<IActionResult> OnPostSettleAsync(int id, string to)
    {
        var status = to == "waive" ? FineStatus.Waived : FineStatus.Collected;
        var err = await Svc<FineService>().SettleAsync(id, status, BranchRestriction ?? await StaffBranchIdAsync());
        if (err != null) FlashError = err; else Flash = status == FineStatus.Waived ? "Fine waived." : "Fine collected.";
        return RedirectToPage(new { Q, Status });
    }
}
