using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

public record OpResult(bool Ok, string Message)
{
    public static OpResult Success(string m) => new(true, m);
    public static OpResult Fail(string m) => new(false, m);
}

/// <summary>
/// Spec section 4. EVERY item status change goes through this class (or LendingService) so that waitlists
/// and notifications are always handled – pages never set Item.Status directly.
/// Each public method commits with a single SaveChanges (= one transaction).
/// </summary>
public class ItemLifecycleService(AppDbContext db, SettingsService settings, NotificationService notify, FineService fines)
{
    void History(Item item, string text, string? by) =>
        db.ItemEvents.Add(new ItemEvent { ItemId = item.Id, Item = item, At = Clock.Now, Text = text, By = by });

    static void Touch(Item item) => item.Version++;

    /// <summary>
    /// Transitions 2/3, 6/7, 8, 10/11, 12: the item has just become free. If somebody is waiting it becomes
    /// Reserved for the first patron in queue order, otherwise Available. (F5 main flow, step 4.)
    /// </summary>
    public async Task<Reservation?> AssignToQueueOrFreeAsync(Item item, LibrarySettings s, string? by)
    {
        var waiting = await db.Reservations.Include(r => r.Patron)
            .Where(r => r.Isbn == item.Isbn && r.Status == HoldStatus.Waiting)
            .OrderBy(r => r.QueueKey).ToListAsync();
        // materialised + filtered locally so two assignments in one unit of work never pick the same hold
        var next = waiting.FirstOrDefault(r => r.Status == HoldStatus.Waiting);

        Touch(item);
        if (next == null)
        {
            item.Status = ItemStatus.Available;
            item.StatusNote = null;
            History(item, $"Available at {(await db.Branches.FindAsync(item.CurrentBranchId))?.Name}", by);
            return null;
        }

        item.Status = ItemStatus.Reserved;
        item.StatusNote = null;
        next.Status = HoldStatus.Ready;
        next.ReadyItem = item;                       // navigation so it also works for a not-yet-saved new copy
        next.ReadyItemId = item.Id > 0 ? item.Id : null;
        next.ReadyAt = Clock.Now;
        next.ExpiresAt = Clock.Today.AddDays(s.PickupDays);
        next.PickupBranchId = item.CurrentBranchId; // collect where the copy actually is
        next.PickupCode = "LH" + Random.Shared.Next(1000, 9999);
        History(item, $"Reserved for {next.Patron.FullName} until {Fmt.Day(next.ExpiresAt.Value)}", by);
        await notify.HoldReadyAsync(next, item);
        return next;
    }

    /// <summary>Called after a brand-new copy is added or imported so waiting patrons are served.</summary>
    public async Task OnItemAddedAsync(Item item, string? by)
    {
        var s = await settings.LoadAsync();
        History(item, "Added to catalogue", by);
        if (item.Status == ItemStatus.Available &&
            await db.Reservations.AnyAsync(r => r.Isbn == item.Isbn && r.Status == HoldStatus.Waiting))
            await AssignToQueueOrFreeAsync(item, s, by);
    }

    // ---------------------------------------------------------------- returns
    public async Task<OpResult> CheckInAsync(string code, int deskBranchId, bool damaged, string by, int? deskId)
    {
        var s = await settings.LoadAsync();
        code = code.Trim();
        var item = await db.Items.Include(i => i.CurrentBranch).FirstOrDefaultAsync(i => i.Code == code);
        if (item == null) return OpResult.Fail($"No item with code {code}.");

        var loan = await db.Loans.Include(l => l.Patron)
            .FirstOrDefaultAsync(l => l.ItemId == item.Id && l.ReturnedAt == null);
        if (loan == null) return OpResult.Fail($"“{item.Title}” is not on loan (status: {Fmt.StatusText(item.Status)}).");

        loan.ReturnedAt = Clock.Now;
        await fines.UpsertForLoanAsync(loan, s, deskId);
        item.CurrentBranchId = deskBranchId; // returned at another branch → flagged "away from home"
        var msg = $"Returned “{item.Title}” from {loan.Patron.FullName}.";
        var late = loan.DaysLate(Clock.Now);
        if (late > 0) msg += $" {late} day(s) late – fine {Fmt.Money(late * s.FinePerDayCents)}.";

        if (damaged)
        {
            item.Status = ItemStatus.Damaged; // transition 5 – queue stays open, nobody is notified until repair
            item.StatusNote = $"In repair · est. back {Fmt.Day(Clock.Today.AddDays(14))}";
            Touch(item);
            History(item, "Returned damaged", by);
            msg += " Marked Damaged.";
        }
        else
        {
            History(item, $"Returned by {loan.Patron.FullName}", by);
            var hold = await AssignToQueueOrFreeAsync(item, s, by);
            if (hold != null) msg += $" Reserved for {hold.Patron.FullName} (hold ready).";
        }
        await db.SaveChangesAsync();
        return OpResult.Success(msg);
    }

    // ---------------------------------------------------------------- damage / repair
    public async Task<OpResult> MarkDamagedAsync(int itemId, string by)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item == null) return OpResult.Fail("Item not found.");
        if (item.Status != ItemStatus.Available) return OpResult.Fail("Only Available items can be marked damaged.");
        item.Status = ItemStatus.Damaged; Touch(item);
        item.StatusNote = $"In repair · est. back {Fmt.Day(Clock.Today.AddDays(14))}";
        History(item, "Marked damaged", by);
        await db.SaveChangesAsync();
        return OpResult.Success($"“{item.Title}” marked damaged.");
    }

    public async Task<OpResult> RepairDoneAsync(int itemId, string by)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item == null) return OpResult.Fail("Item not found.");
        if (item.Status != ItemStatus.Damaged) return OpResult.Fail("Only Damaged items can be repaired.");
        var s = await settings.LoadAsync();
        History(item, "Repair complete", by);
        var hold = await AssignToQueueOrFreeAsync(item, s, by); // transitions 6 / 7
        await db.SaveChangesAsync();
        return OpResult.Success(hold == null ? "Repair done – item is Available." : $"Repair done – reserved for {hold.Patron.FullName}.");
    }

    // ---------------------------------------------------------------- F4 transfers
    public async Task<OpResult> CreateTransferAsync(string code, int toBranchId, string reason, string by, int? actorBranchId)
    {
        var item = await db.Items.Include(i => i.CurrentBranch).FirstOrDefaultAsync(i => i.Code == code.Trim());
        if (item == null) return OpResult.Fail($"No item with code {code}.");
        if (item.Status != ItemStatus.Available)
            return OpResult.Fail($"Transfer refused: “{item.Title}” is {Fmt.StatusText(item.Status)} – only Available items can be transferred.");
        if (item.CurrentBranchId == toBranchId) return OpResult.Fail("The item is already at that branch.");
        if (actorBranchId != null && item.CurrentBranchId != actorBranchId)
            return OpResult.Fail($"This item is at {item.CurrentBranch.Name}; only that branch can send it.");
        if (await db.Transfers.AnyAsync(t => t.ItemId == item.Id && (t.Status == TransferStatus.Requested || t.Status == TransferStatus.Dispatched)))
            return OpResult.Fail("This item already has an open transfer.");

        var t = new BranchTransfer
        {
            ItemId = item.Id, FromBranchId = item.CurrentBranchId, ToBranchId = toBranchId, Reason = reason,
            Status = TransferStatus.Requested, RequestedAt = Clock.Now, RequestedBy = by
        };
        db.Transfers.Add(t);
        History(item, $"Transfer requested to {(await db.Branches.FindAsync(toBranchId))?.Name}", by);
        await db.SaveChangesAsync();
        return OpResult.Success($"Transfer {t.Number} created for “{item.Title}”.");
    }

    public async Task<OpResult> BulkTransferAsync(IEnumerable<int> itemIds, int toBranchId, string by, int? actorBranchId)
    {
        int ok = 0; var errors = new List<string>();
        foreach (var id in itemIds)
        {
            var code = await db.Items.Where(i => i.Id == id).Select(i => i.Code).FirstOrDefaultAsync();
            if (code == null) continue;
            var r = await CreateTransferAsync(code, toBranchId, "Rebalancing", by, actorBranchId);
            if (r.Ok) ok++; else errors.Add(r.Message);
        }
        return new OpResult(ok > 0, $"{ok} transfer(s) created" + (errors.Count > 0 ? $", {errors.Count} refused." : "."));
    }

    public async Task<OpResult> DispatchAsync(int transferId, string by, int? actorBranchId)
    {
        var t = await db.Transfers.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == transferId);
        if (t == null) return OpResult.Fail("Transfer not found.");
        if (t.Status != TransferStatus.Requested) return OpResult.Fail("Only Requested transfers can be dispatched.");
        if (actorBranchId != null && actorBranchId != t.FromBranchId) return OpResult.Fail("Only the sending desk can dispatch.");
        if (t.Item.Status != ItemStatus.Available) return OpResult.Fail($"Item is now {Fmt.StatusText(t.Item.Status)} – cannot dispatch.");

        t.Status = TransferStatus.Dispatched; t.DispatchedAt = Clock.Now; t.DispatchedBy = by;
        t.Item.Status = ItemStatus.InTransit; Touch(t.Item); // transition 9
        History(t.Item, "Dispatched", by);
        await db.SaveChangesAsync();
        return OpResult.Success($"{t.Number} dispatched.");
    }

    public async Task<OpResult> ReceiveAsync(int transferId, string? scannedCode, string by, int? actorBranchId)
    {
        var t = await db.Transfers.Include(x => x.Item).Include(x => x.ToBranch).Include(x => x.FromBranch)
            .FirstOrDefaultAsync(x => x.Id == transferId);
        if (t == null) return OpResult.Fail("Transfer not found.");
        if (t.Status != TransferStatus.Dispatched) return OpResult.Fail("Only Dispatched transfers can be received.");
        if (actorBranchId != null && actorBranchId != t.ToBranchId) return OpResult.Fail("Only the destination desk can receive.");
        if (!string.IsNullOrWhiteSpace(scannedCode) && !string.Equals(scannedCode.Trim(), t.Item.Code, StringComparison.OrdinalIgnoreCase))
            return OpResult.Fail("Wrong item scanned – status unchanged.");

        var s = await settings.LoadAsync();
        t.Status = TransferStatus.Received; t.ReceivedAt = Clock.Now; t.ReceivedBy = by;
        t.Item.CurrentBranchId = t.ToBranchId;
        History(t.Item, $"Received at {t.ToBranch.Name}", by);
        var hold = await AssignToQueueOrFreeAsync(t.Item, s, by); // transitions 10 / 11
        notify.TransferArrived(t, t.Item, t.ToBranch, t.FromBranch);
        await db.SaveChangesAsync();
        return OpResult.Success(hold == null ? $"{t.Number} received – item Available." : $"{t.Number} received – reserved for {hold.Patron.FullName}.");
    }

    public async Task<OpResult> CancelTransferAsync(int transferId, string by)
    {
        var t = await db.Transfers.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == transferId);
        if (t == null) return OpResult.Fail("Transfer not found.");
        if (t.Status != TransferStatus.Requested) return OpResult.Fail("Only Requested transfers can be cancelled.");
        t.Status = TransferStatus.Cancelled;
        History(t.Item, $"Transfer {t.Number} cancelled", by);
        await db.SaveChangesAsync();
        return OpResult.Success($"{t.Number} cancelled.");
    }

    // ---------------------------------------------------------------- F5 holds
    public async Task<int> PositionAsync(Reservation r) =>
        r.Status != HoldStatus.Waiting ? 0
        : 1 + await db.Reservations.CountAsync(x => x.Isbn == r.Isbn && x.Status == HoldStatus.Waiting && x.QueueKey < r.QueueKey);

    public async Task<OpResult> JoinWaitlistAsync(AppUser patron, string isbn, int pickupBranchId)
    {
        var s = await settings.LoadAsync();
        var copies = await db.Items.Where(i => i.Isbn == isbn).ToListAsync();
        if (copies.Count == 0) return OpResult.Fail("Title not found.");
        if (copies.Any(c => c.Status == ItemStatus.Available))
            return OpResult.Fail("A copy is available now – borrow it at the desk or kiosk.");

        var existing = await db.Reservations.FirstOrDefaultAsync(r => r.PatronId == patron.Id && r.Isbn == isbn &&
            (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
        if (existing != null)
            return OpResult.Fail(existing.Status == HoldStatus.Ready
                ? "Your hold on this title is ready for pickup."
                : $"You are already in the queue at position #{await PositionAsync(existing)}.");

        var active = await db.Reservations.CountAsync(r => r.PatronId == patron.Id &&
            (r.Status == HoldStatus.Waiting || r.Status == HoldStatus.Ready));
        if (active >= s.MaxHolds) return OpResult.Fail($"You have reached the limit of {s.MaxHolds} active holds.");

        var r2 = new Reservation
        {
            Isbn = isbn, TitleText = copies[0].Title, PatronId = patron.Id, PickupBranchId = pickupBranchId,
            PlacedAt = Clock.Now, QueueKey = Clock.Now, Status = HoldStatus.Waiting
        };
        db.Reservations.Add(r2);
        await db.SaveChangesAsync();
        return OpResult.Success($"You are #{await PositionAsync(r2)} in the queue for “{r2.TitleText}”.");
    }

    public async Task<OpResult> CancelReservationAsync(int id, int? onlyForPatronId, string by)
    {
        var r = await db.Reservations.Include(x => x.Patron).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null || (onlyForPatronId != null && r.PatronId != onlyForPatronId)) return OpResult.Fail("Hold not found.");
        if (!r.IsActive) return OpResult.Fail("That hold is already closed.");

        var wasReady = r.Status == HoldStatus.Ready;
        r.Status = HoldStatus.Cancelled;
        if (wasReady && r.ReadyItemId != null) await ReleaseReservedItemAsync(r.ReadyItemId.Value, by); // transition 8/12
        await db.SaveChangesAsync();
        return OpResult.Success($"Hold on “{r.TitleText}” cancelled.");
    }

    async Task ReleaseReservedItemAsync(int itemId, string? by)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item is { Status: ItemStatus.Reserved })
            await AssignToQueueOrFreeAsync(item, await settings.LoadAsync(), by);
    }

    public async Task<OpResult> MoveToFrontAsync(int id, string by)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null || r.Status != HoldStatus.Waiting) return OpResult.Fail("Only waiting holds can be moved.");
        var earliest = (await db.Reservations.Where(x => x.Isbn == r.Isbn && x.Status == HoldStatus.Waiting)
            .Select(x => x.QueueKey).ToListAsync()).Min();
        r.QueueKey = earliest.AddSeconds(-1);
        await db.SaveChangesAsync();
        return OpResult.Success($"Moved to front of the queue (audited: {by}, {Fmt.Time(Clock.Now)}).");
    }

    public async Task<OpResult> ChangePickupBranchAsync(int id, int patronId, int branchId)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id && x.PatronId == patronId);
        if (r == null || r.Status != HoldStatus.Waiting) return OpResult.Fail("Pickup branch can only be changed while the hold is Waiting.");
        r.PickupBranchId = branchId;
        await db.SaveChangesAsync();
        return OpResult.Success("Pickup branch updated.");
    }

    public async Task<OpResult> MarkPickedUpAsync(int id)
    {
        var r = await db.Reservations.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null || r.Status != HoldStatus.Ready) return OpResult.Fail("Hold is not Ready.");
        var onLoan = r.ReadyItemId != null && await db.Loans.AnyAsync(l => l.ItemId == r.ReadyItemId && l.PatronId == r.PatronId && l.ReturnedAt == null);
        if (!onLoan) return OpResult.Fail("Check the item out to the patron first – the hold is then fulfilled automatically.");
        r.Status = HoldStatus.Fulfilled;
        await db.SaveChangesAsync();
        return OpResult.Success("Hold marked as picked up.");
    }

    /// <summary>F5: Ready holds past their collect-by date expire and the item goes to the next patron.</summary>
    public async Task<int> ExpireHoldsAsync()
    {
        var s = await settings.LoadAsync();
        var expired = await db.Reservations.Where(r => r.Status == HoldStatus.Ready && r.ExpiresAt != null && r.ExpiresAt < Clock.Today).ToListAsync();
        foreach (var r in expired)
        {
            r.Status = HoldStatus.Expired;
            if (r.ReadyItemId != null)
            {
                var item = await db.Items.FindAsync(r.ReadyItemId);
                if (item is { Status: ItemStatus.Reserved }) await AssignToQueueOrFreeAsync(item, s, "system");
            }
        }
        if (expired.Count > 0) await db.SaveChangesAsync();
        return expired.Count;
    }

    // ---------------------------------------------------------------- renewals
    public async Task<OpResult> RenewAsync(int loanId, int? patronId)
    {
        var s = await settings.LoadAsync();
        var loan = await db.Loans.Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == loanId && l.ReturnedAt == null);
        if (loan == null || (patronId != null && loan.PatronId != patronId)) return OpResult.Fail("Loan not found.");
        if (await db.Reservations.AnyAsync(r => r.Isbn == loan.Item.Isbn && r.Status == HoldStatus.Waiting))
            return OpResult.Fail($"“{loan.Item.Title}” has a waitlist and cannot be renewed.");
        loan.DueAt = (loan.DueAt > Clock.Today ? loan.DueAt : Clock.Today).AddDays(s.LoanDays);
        loan.Renewals++;
        await db.SaveChangesAsync();
        return OpResult.Success($"“{loan.Item.Title}” renewed – now due {Fmt.DayLong(loan.DueAt)}.");
    }
}
