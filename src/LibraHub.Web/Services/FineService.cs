using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

/// <summary>Fines accrue per overdue day on active loans and are frozen once collected or waived.</summary>
public class FineService(AppDbContext db, SettingsService settings)
{
    /// <summary>Creates/updates the fine row for a loan (does not save).</summary>
    public async Task UpsertForLoanAsync(Loan loan, LibrarySettings s, int? deskId = null)
    {
        var days = loan.DaysLate(Clock.Now);
        if (days <= 0) return;
        var fine = await db.Fines.FirstOrDefaultAsync(f => f.LoanId == loan.Id);
        if (fine == null)
        {
            db.Fines.Add(new Fine
            {
                LoanId = loan.Id, PatronId = loan.PatronId, DeskId = deskId, DaysLate = days,
                AmountCents = days * s.FinePerDayCents, IssuedAt = Clock.Now, Status = FineStatus.Outstanding
            });
        }
        else if (fine.Status == FineStatus.Outstanding)
        {
            fine.DaysLate = days;
            fine.AmountCents = days * s.FinePerDayCents;
        }
    }

    public async Task AccrueAllAsync()
    {
        var s = await settings.LoadAsync();
        var overdue = await db.Loans.Where(l => l.ReturnedAt == null && l.DueAt < Clock.Today).ToListAsync();
        foreach (var l in overdue) await UpsertForLoanAsync(l, s);
    }

    public async Task<int> OutstandingCentsAsync(int patronId)
    {
        var s = await settings.LoadAsync();
        var overdue = await db.Loans.Where(l => l.PatronId == patronId && l.ReturnedAt == null && l.DueAt < Clock.Today).ToListAsync();
        foreach (var l in overdue) await UpsertForLoanAsync(l, s);
        await db.SaveChangesAsync();
        return await db.Fines.Where(f => f.PatronId == patronId && f.Status == FineStatus.Outstanding).SumAsync(f => f.AmountCents);
    }

    public async Task<string?> SettleAsync(int fineId, FineStatus to, int? deskId)
    {
        var f = await db.Fines.FindAsync(fineId);
        if (f == null) return "Fine not found.";
        if (f.Status != FineStatus.Outstanding) return "That fine is already settled.";
        f.Status = to;
        f.SettledAt = Clock.Now;
        f.DeskId ??= deskId;
        await db.SaveChangesAsync();
        return null;
    }
}
