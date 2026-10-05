using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

/// <summary>Spec appendix 6.1 – message templates.</summary>
public static class MessageTemplates
{
    public record Msg(string Subject, string Body, string Sms);

    public static Msg Borrow(string name, string title, string branch, DateTime due)
    {
        var d = Fmt.DayLong(due);
        return new($"Your LibraHub receipt — due {d}",
            $"Hi {name}, you borrowed {title} from {branch} today. It is due on {d}. Renew online unless someone is waiting.",
            $"LibraHub: {title} due {d}.");
    }

    public static Msg DueSoon(string name, string title, string when, string branch)
    {
        var body = $"Hi {name}, {title} is due {when} at {branch}. Renew or return it to avoid fines.";
        return new($"{title} is due {when}", body, body);
    }

    public static Msg Overdue(string name, string title, int days, int fineCents)
    {
        var body = $"Hi {name}, {title} is {days} days overdue. Current fine: {Fmt.Money(fineCents)}. Please return it.";
        return new($"{title} is overdue", body, body);
    }

    public static Msg HoldReady(string name, string title, string pickupBranch, DateTime expires)
    {
        var body = $"Hi {name}, {title} is ready at {pickupBranch}. Collect by {Fmt.Day(expires)}.";
        return new("Your hold is ready", body, body);
    }

    public static Msg Transfer(string title, string branch, string from) =>
        new("Transfer received", $"{title} arrived at {branch} from {from}.", $"{title} arrived at {branch} from {from}.");
}

/// <summary>
/// Feature F3 – simulated email/SMS. Rows are ADDED to the DbContext but not saved; the caller saves,
/// so a business operation and its notifications commit in the same transaction.
/// </summary>
public class NotificationService(AppDbContext db, SettingsService settings, FineService fines, ILogger<NotificationService> log)
{
    public static Channels PrefFor(AppUser p, NotificationType t) => t switch
    {
        NotificationType.Borrow => p.PrefBorrow,
        NotificationType.DueSoon => p.PrefDueSoon,
        NotificationType.Overdue => p.PrefOverdue,
        _ => p.PrefHold
    };

    public async Task<int> QueueAsync(NotificationType type, AppUser patron, MessageTemplates.Msg msg,
        string? uniqueKey = null, Channels? overrideChannels = null)
    {
        var s = await settings.LoadAsync();
        var channels = overrideChannels ?? PrefFor(patron, type);
        var added = 0;
        foreach (var ch in new[] { Channel.Email, Channel.Sms })
        {
            var flag = ch == Channel.Email ? Channels.Email : Channels.Sms;
            if ((channels & flag) == 0) continue; // patron opted out of this channel → no row

            var key = uniqueKey == null ? null : $"{uniqueKey}:{ch}";
            if (key != null && (db.Notifications.Local.Any(n => n.UniqueKey == key)
                                || await db.Notifications.AnyAsync(n => n.UniqueKey == key)))
                continue; // idempotent

            var failed = ch == Channel.Sms && string.IsNullOrWhiteSpace(patron.Mobile);
            var row = new NotificationLog
            {
                PatronId = patron.Id,
                Recipient = ch == Channel.Email ? patron.Email : (patron.Mobile ?? "(no mobile)"),
                Type = type,
                Channel = ch,
                Subject = ch == Channel.Sms ? "—" : msg.Subject,
                Body = ch == Channel.Sms ? msg.Sms : msg.Body,
                Status = failed ? SendStatus.SimulatedFailed : SendStatus.SimulatedSent,
                FailureReason = failed ? "No mobile number on file" : null,
                CreatedAt = Clock.Now,
                UniqueKey = key
            };
            db.Notifications.Add(row);
            added++;
            if (s.ConsoleLogging)
                log.LogInformation("[SIMULATED {Channel}] {Type} → {To}: {Subject} | {Body} | {Status}",
                    ch, type, row.Recipient, row.Subject, row.Body, row.Status);
        }
        return added;
    }

    public Task<int> BorrowAsync(AppUser patron, Item item, Branch branch, DateTime due, Channels? over = null)
        => QueueAsync(NotificationType.Borrow, patron,
            MessageTemplates.Borrow(patron.FullName, item.Title, branch.Name, due),
            overrideChannels: over);

    public async Task HoldReadyAsync(Reservation r, Item item)
    {
        var branch = await db.Branches.FindAsync(r.PickupBranchId);
        var patron = r.Patron ?? await db.Users.FindAsync(r.PatronId) ?? throw new InvalidOperationException();
        await QueueAsync(NotificationType.HoldAvailable, patron,
            MessageTemplates.HoldReady(patron.FullName, item.Title, branch?.Name ?? "the library", r.ExpiresAt ?? Clock.Today),
            uniqueKey: $"hold{r.Id}:ready:{Clock.Today:yyyyMMdd}");
    }

    /// <summary>Staff-facing "Transfer arrived" row (spec 6.1, channel "Staff view").</summary>
    public void TransferArrived(BranchTransfer t, Item item, Branch to, Branch from)
    {
        var m = MessageTemplates.Transfer(item.Title, to.Name, from.Name);
        db.Notifications.Add(new NotificationLog
        {
            PatronId = null, Recipient = $"Staff · {to.Name}", Type = NotificationType.Transfer, Channel = Channel.Email,
            Subject = m.Subject, Body = m.Body, Status = SendStatus.SimulatedSent, CreatedAt = Clock.Now
        });
    }

    /// <summary>Manual "Remind" action on the staff dashboard (mockup 7 #4).</summary>
    public async Task RemindAsync(Loan loan)
    {
        var s = await settings.LoadAsync();
        var days = loan.DaysLate(Clock.Now);
        var cents = days * s.FinePerDayCents;
        await QueueAsync(NotificationType.Overdue, loan.Patron,
            MessageTemplates.Overdue(loan.Patron.FullName, loan.Item.Title, days, cents),
            overrideChannels: loan.Patron.PrefOverdue == Channels.None ? Channels.Email : loan.Patron.PrefOverdue);
    }

    public record ScanResult(int DueSoon, int Overdue, int Skipped);

    /// <summary>
    /// The due-soon / overdue scanner (spec F3 rules): due-soon once per loan, overdue on day 1 and then every 7 days.
    /// Safe to run repeatedly – unique keys stop duplicates.
    /// </summary>
    public async Task<ScanResult> RunScanAsync()
    {
        var s = await settings.LoadAsync();
        var today = Clock.Today;
        int dueSoon = 0, overdue = 0, skipped = 0;

        var loans = await db.Loans.Include(l => l.Patron).Include(l => l.Item).ThenInclude(i => i.CurrentBranch)
            .Where(l => l.ReturnedAt == null).ToListAsync();

        foreach (var l in loans)
        {
            var daysToDue = (l.DueAt.Date - today).Days;
            if (daysToDue >= 0 && daysToDue <= s.DueSoonDays)
            {
                var when = daysToDue == 0 ? "today" : daysToDue == 1 ? "tomorrow" : $"on {Fmt.Day(l.DueAt)}";
                var n = await QueueAsync(NotificationType.DueSoon, l.Patron,
                    MessageTemplates.DueSoon(l.Patron.FullName, l.Item.Title, when, l.Item.CurrentBranch.Name),
                    uniqueKey: $"loan{l.Id}:duesoon");
                if (n > 0) dueSoon++; else skipped++;
            }
            else if (daysToDue < 0)
            {
                var late = -daysToDue;
                if ((late - 1) % 7 != 0) continue;
                var n = await QueueAsync(NotificationType.Overdue, l.Patron,
                    MessageTemplates.Overdue(l.Patron.FullName, l.Item.Title, late, late * s.FinePerDayCents),
                    uniqueKey: $"loan{l.Id}:overdue:{today:yyyyMMdd}");
                if (n > 0) overdue++; else skipped++;
            }
        }

        await fines.AccrueAllAsync();
        await db.SaveChangesAsync();
        return new ScanResult(dueSoon, overdue, skipped);
    }
}
