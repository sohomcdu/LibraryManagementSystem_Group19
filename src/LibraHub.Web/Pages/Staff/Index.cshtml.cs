using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Mockup 7 – reception/manager dashboard.</summary>
public class IndexModel : AppPageModel
{
    public int LoansToday, ReturnsToday, OverdueItems, HoldsReady, TransfersOpen, LoansYesterday;
    public List<(int LoanId, Item Item, AppUser Patron, int Days)> Overdue = new();
    public List<Reservation> Ready = new();
    public List<BranchTransfer> Incoming = new();
    public List<NotificationLog> Recent = new();

    public async Task OnGetAsync()
    {
        var branchId = await StaffBranchIdAsync();
        var today = Clock.Today;

        LoansToday = await Db.Loans.CountAsync(l => l.DeskId != null && l.Desk!.BranchId == branchId && l.BorrowedAt >= today);
        LoansYesterday = await Db.Loans.CountAsync(l => l.DeskId != null && l.Desk!.BranchId == branchId && l.BorrowedAt >= today.AddDays(-1) && l.BorrowedAt < today);
        ReturnsToday = await Db.Loans.CountAsync(l => l.Item.CurrentBranchId == branchId && l.ReturnedAt >= today);
        OverdueItems = await Db.Loans.CountAsync(l => l.ReturnedAt == null && l.DueAt < today && l.Item.CurrentBranchId == branchId);
        HoldsReady = await Db.Reservations.CountAsync(r => r.Status == HoldStatus.Ready && r.PickupBranchId == branchId);
        TransfersOpen = await Db.Transfers.CountAsync(t => (t.Status == TransferStatus.Requested || t.Status == TransferStatus.Dispatched) && (t.FromBranchId == branchId || t.ToBranchId == branchId));

        var overdueLoans = await Db.Loans.Include(l => l.Item).Include(l => l.Patron)
            .Where(l => l.ReturnedAt == null && l.DueAt < today && l.Item.CurrentBranchId == branchId)
            .OrderByDescending(l => l.DueAt).Take(5).ToListAsync();
        Overdue = overdueLoans.Select(l => (l.Id, l.Item, l.Patron, (today - l.DueAt.Date).Days)).ToList();

        Ready = await Db.Reservations.Include(r => r.Patron).Where(r => r.Status == HoldStatus.Ready && r.PickupBranchId == branchId)
            .OrderBy(r => r.ExpiresAt).Take(5).ToListAsync();
        Incoming = await Db.Transfers.Include(t => t.Item).Include(t => t.FromBranch)
            .Where(t => t.ToBranchId == branchId && (t.Status == TransferStatus.Requested || t.Status == TransferStatus.Dispatched))
            .OrderBy(t => t.RequestedAt).Take(5).ToListAsync();
        Recent = await Db.Notifications.OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync();
    }
}
