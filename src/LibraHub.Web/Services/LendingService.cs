using System.Text.RegularExpressions;
using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

public record PatronSummary(AppUser Patron, int Loans, int Limit, int Holds, int ReadyHolds, int FineCents, bool FineBlocked);

/// <summary>One scanned line in the desk / kiosk basket. Serialised into the server session (F1: never in URLs).</summary>
public class BasketLine
{
    public int ItemId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Branch { get; set; } = "";
    public DateTime? Due { get; set; }
    public ItemStatus Status { get; set; }
    public bool Ok { get; set; }
    public string? Problem { get; set; }
}

public record ScanOutcome(BasketLine Line, string? Error, string? Warning, bool OfferTransfer);

/// <summary>
/// Shared lending rules used by the reception desk (mockup 8) and the kiosk (mockups 19-22):
/// "Kiosk uses the same loan rules as the reception desk" (F1).
/// </summary>
public class LendingService(AppDbContext db, SettingsService settings, FineService fines, NotificationService notify)
{
    /// <summary>Accepts "LC-2048-7731", "2048-7731" or "20487731".</summary>
    public static string NormalizeCard(string input)
    {
        var t = (input ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        var m = Regex.Match(t, @"^(?:LC-?)?(\d{4})-?(\d{4})$");
        return m.Success ? $"LC-{m.Groups[1].Value}-{m.Groups[2].Value}" : t;
    }

    public Task<AppUser?> FindPatronAsync(string card)
    {
        var c = NormalizeCard(card);
        return db.Users.Include(u => u.HomeBranch).FirstOrDefaultAsync(u => u.CardNumber == c && u.Role == Role.Patron);
    }

    public async Task<PatronSummary> SummaryAsync(AppUser p)
    {
        var s = await settings.LoadAsync();
        var loans = await db.Loans.CountAsync(l => l.PatronId == p.Id && l.ReturnedAt == null);
        var holds = await db.Reservations.CountAsync(r => r.PatronId == p.Id && (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
        var ready = await db.Reservations.CountAsync(r => r.PatronId == p.Id && r.Status == HoldStatus.Ready);
        var fine = await fines.OutstandingCentsAsync(p.Id);
        return new PatronSummary(p, loans, s.MaxLoans, holds, ready, fine, fine > s.FineBlockCents);
    }

    /// <summary>Validate one scanned code against every business rule (F1 main-flow step 4).</summary>
    public async Task<ScanOutcome> ScanAsync(AppUser patron, string code, int branchId, IReadOnlyCollection<int> basketIds)
    {
        var s = await settings.LoadAsync();
        code = (code ?? "").Trim();
        var item = await db.Items.Include(i => i.CurrentBranch).FirstOrDefaultAsync(i => i.Code == code);
        if (item == null)
            return new(new BasketLine { Code = code, Title = "Unknown item", Ok = false }, $"No item found with code {code}.", null, false);

        var line = new BasketLine
        {
            ItemId = item.Id, Code = item.Code, Title = item.Title, Branch = item.CurrentBranch.Name,
            Status = item.Status, Ok = false
        };
        string? Fail(string msg) { line.Problem = msg; return msg; }

        if (basketIds.Contains(item.Id)) return new(line, Fail("That item is already in the basket."), null, false);

        switch (item.Status)
        {
            case ItemStatus.InTransit:
                return new(line, Fail($"“{item.Title}” is in transit and cannot be borrowed."), null, false);
            case ItemStatus.Damaged:
                return new(line, Fail($"“{item.Title}” is damaged and awaiting repair."), null, false);
            case ItemStatus.Borrowed:
                return new(line, Fail($"“{item.Title}” is already on loan."), null, false);
            case ItemStatus.Reserved:
                var hold = await db.Reservations.FirstOrDefaultAsync(r => r.ReadyItemId == item.Id && r.Status == HoldStatus.Ready);
                if (hold == null || hold.PatronId != patron.Id)
                    return new(line, Fail($"“{item.Title}” is reserved for another patron (queue #1) and cannot be checked out."), null, false);
                break; // reserved for this patron → allowed (transition 4)
        }

        var active = await db.Loans.CountAsync(l => l.PatronId == patron.Id && l.ReturnedAt == null);
        if (active + basketIds.Count >= s.MaxLoans)
            return new(line, Fail($"Loan limit reached ({s.MaxLoans} items). Items already in the basket can still be checked out."), null, false);

        line.Ok = true;
        line.Due = Clock.Today.AddDays(s.LoanDays);
        string? warning = null; var offer = false;
        if (item.CurrentBranchId != branchId)
        {
            var mine = (await db.Branches.FindAsync(branchId))?.Name ?? "this branch";
            warning = $"{item.Code} belongs to {item.CurrentBranch.Name} — record a transfer to {mine}?";
            offer = item.Status == ItemStatus.Available;
        }
        return new(line, null, warning, offer);
    }

    /// <summary>Creates all loans in ONE transaction. Returns an error text or null.</summary>
    public async Task<(List<Loan> Loans, string? Error)> CheckOutAsync(
        AppUser patron, IEnumerable<int> itemIds, int deskId, Channels? receipt, string by)
    {
        var s = await settings.LoadAsync();
        var desk = await db.Desks.Include(d => d.Branch).FirstAsync(d => d.Id == deskId);
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return (new(), "The basket is empty.");

        var fine = await fines.OutstandingCentsAsync(patron.Id);
        if (fine > s.FineBlockCents)
            return (new(), $"Outstanding fines of {Fmt.Money(fine)} must be collected or waived before check-out.");

        var active = await db.Loans.CountAsync(l => l.PatronId == patron.Id && l.ReturnedAt == null);
        if (active + ids.Count > s.MaxLoans) return (new(), $"Loan limit of {s.MaxLoans} items would be exceeded.");

        var items = await db.Items.Include(i => i.CurrentBranch).Where(i => ids.Contains(i.Id)).ToListAsync();
        var holds = await db.Reservations.Where(r => r.Status == HoldStatus.Ready && r.ReadyItemId != null && ids.Contains(r.ReadyItemId.Value)).ToListAsync();

        // pass 1 – re-validate (state may have changed since the scan)
        foreach (var i in items)
        {
            var hold = holds.FirstOrDefault(h => h.ReadyItemId == i.Id);
            var ok = i.Status == ItemStatus.Available || (i.Status == ItemStatus.Reserved && hold?.PatronId == patron.Id);
            if (!ok) return (new(), $"“{i.Title}” is no longer available ({Fmt.StatusText(i.Status)}). Please scan again.");
        }

        // pass 2 – mutate
        var loans = new List<Loan>();
        foreach (var i in items)
        {
            var loan = new Loan
            {
                ItemId = i.Id, PatronId = patron.Id, DeskId = deskId,
                BorrowedAt = Clock.Now, DueAt = Clock.Today.AddDays(s.LoanDays)
            };
            db.Loans.Add(loan); loans.Add(loan);
            holds.FirstOrDefault(h => h.ReadyItemId == i.Id)!.Let(h => h.Status = HoldStatus.Fulfilled); // transition 4
            i.Status = ItemStatus.Borrowed;                 // transition 1 / 4
            i.CurrentBranchId = desk.BranchId;              // kiosk/desk policy: current branch follows the loan
            i.Version++;
            db.ItemEvents.Add(new ItemEvent { Item = i, At = Clock.Now, Text = $"Borrowed by {patron.FullName} ({desk.Name})", By = by });
            await notify.BorrowAsync(patron, i, desk.Branch, loan.DueAt, receipt);
        }

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return (new(), "Another user changed one of these items at the same moment. Please scan again.");
        }
        return (loans, null);
    }
}

static class ObjectExt
{
    public static void Let<T>(this T? o, Action<T> a) where T : class { if (o != null) a(o); }
}
