using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Pages.Staff;

/// <summary>Mockup 8 – Desk: check-out / check-in / renew. Basket lives in session, never in the URL.</summary>
public class DeskModel : AppPageModel
{
    const string SessionKey = "desk.basket";
    const string PatronKey = "desk.patron";

    [BindProperty] public string? Card { get; set; }
    [BindProperty] public string? ItemCode { get; set; }
    [BindProperty] public bool ReceiptEmail { get; set; } = true;
    [BindProperty] public bool ReceiptSms { get; set; }
    [BindProperty] public string Tab { get; set; } = "checkout";

    public AppUser? Patron { get; private set; }
    public PatronSummary? Summary { get; private set; }
    public List<BasketLine> Basket => HttpContext.Session.GetObj<List<BasketLine>>(SessionKey) ?? new();
    public string? Warning { get; private set; }
    public bool OfferTransfer { get; private set; }
    public int Branch { get; private set; }

    async Task LoadPatronAsync()
    {
        var id = HttpContext.Session.GetInt32(PatronKey);
        if (id != null)
            Patron = await Db.Users.Include(u => u.HomeBranch).FirstOrDefaultAsync(u => u.Id == id);
        if (Patron != null) Summary = await Svc<LendingService>().SummaryAsync(Patron);
    }

    public async Task<IActionResult> OnGetAsync(string? tab)
    {
        Tab = tab ?? "checkout";
        Branch = await StaffBranchIdAsync();
        await LoadPatronAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostFindAsync()
    {
        Branch = await StaffBranchIdAsync();
        var p = await Svc<LendingService>().FindPatronAsync(Card ?? "");
        if (p == null) { Err = $"No patron found for \"{Card}\"."; return Page(); }
        HttpContext.Session.SetInt32(PatronKey, p.Id);
        HttpContext.Session.Remove(SessionKey);
        Patron = p; Summary = await Svc<LendingService>().SummaryAsync(p);
        return Page();
    }

    public async Task<IActionResult> OnPostScanAsync()
    {
        Branch = await StaffBranchIdAsync();
        await LoadPatronAsync();
        if (Patron == null) { Err = "Identify the patron first."; return Page(); }
        var basket = Basket;
        var r = await Svc<LendingService>().ScanAsync(
            Patron, ItemCode ?? "", Branch,
            basket.Select(b => b.ItemId).ToList());          // ← .ToList()
        if (r.Error != null) Err = r.Error;
        else
        {
            basket.Add(r.Line);
            HttpContext.Session.SetObj(SessionKey, basket);
            Warning = r.Warning;
            OfferTransfer = r.OfferTransfer;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int itemId)
    {
        var basket = Basket; basket.RemoveAll(b => b.ItemId == itemId);
        HttpContext.Session.SetObj(SessionKey, basket);
        return RedirectToPage(new { tab = "checkout" });
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        HttpContext.Session.Remove(SessionKey);
        return RedirectToPage(new { tab = "checkout" });
    }

    public async Task<IActionResult> OnPostCompleteAsync()
    {
        Branch = await StaffBranchIdAsync();
        await LoadPatronAsync();
        if (Patron == null) { Err = "Identify the patron first."; return Page(); }
        var desk = await Db.Desks.FirstOrDefaultAsync(d => d.BranchId == Branch && !d.IsKiosk)
                   ?? await Db.Desks.FirstAsync(d => d.BranchId == Branch);
        var channels = (ReceiptEmail ? Channels.Email : 0) | (ReceiptSms ? Channels.Sms : 0);

        var itemIds = Basket.Select(b => b.ItemId).ToList();   // ← .ToList()

        var (loans, error) = await Svc<LendingService>().CheckOutAsync(
            Patron, itemIds, desk.Id, channels, CurrentUserName);

        if (error != null) { Err = error; return Page(); }
        HttpContext.Session.Remove(SessionKey);
        Flash = $"Checked out {loans.Count} item(s) to {Patron.FullName}. Due {Fmt.Day(loans.First().DueAt)}.";
        return RedirectToPage(new { tab = "checkout" });
    }

    // ---- check-in
    [BindProperty] public string? CheckInCode { get; set; }
    [BindProperty] public bool MarkDamaged { get; set; }

    public async Task<IActionResult> OnPostCheckInAsync()
    {
        Branch = await StaffBranchIdAsync();
        var r = await Svc<ItemLifecycleService>().CheckInAsync(
            CheckInCode ?? "", Branch, MarkDamaged, CurrentUserName, null);
        Report(r, redirecting: false);
        Tab = "checkin";
        await LoadPatronAsync();
        return Page();
    }

    // ---- renew
    [BindProperty] public string? RenewCard { get; set; }
    [BindProperty] public int RenewLoanId { get; set; }
    public List<Loan> RenewLoans { get; private set; } = new();
    AppUser? _renewPatron;

    public async Task<IActionResult> OnPostRenewFindAsync()
    {
        Branch = await StaffBranchIdAsync();
        var p = await Svc<LendingService>().FindPatronAsync(RenewCard ?? "");
        if (p == null) { Err = "Patron not found."; Tab = "renew"; return Page(); }
        _renewPatron = p; Patron = p;
        Summary = await Svc<LendingService>().SummaryAsync(p);
        RenewLoans = await Db.Loans.Include(l => l.Item)
            .Where(l => l.PatronId == p.Id && l.ReturnedAt == null).ToListAsync();
        Tab = "renew";
        return Page();
    }

    public async Task<IActionResult> OnPostRenewOneAsync(int loanId, string card)
    {
        Branch = await StaffBranchIdAsync();
        var r = await Svc<ItemLifecycleService>().RenewAsync(loanId, null);
        Report(r, redirecting: false);
        var loan = await Db.Loans.FindAsync(loanId);
        if (loan != null)
        {
            Patron = await Db.Users.FindAsync(loan.PatronId);
            if (Patron != null)
            {
                Summary = await Svc<LendingService>().SummaryAsync(Patron);
                RenewLoans = await Db.Loans.Include(l => l.Item)
                    .Where(l => l.PatronId == Patron.Id && l.ReturnedAt == null).ToListAsync();
            }
        }
        Tab = "renew";
        return Page();
    }
}