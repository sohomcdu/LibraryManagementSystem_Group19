using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace LibraHub.Tests;

/// <summary>Exercises the core F1/F5 business rules against a fresh in-memory SQLite database.</summary>
public class FineAndLoanRuleTests : IDisposable
{
    readonly SqliteInMemory _db;
    public FineAndLoanRuleTests() => _db = new SqliteInMemory();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Renew_is_blocked_when_a_waitlist_exists()
    {
        using var ctx = _db.NewContext();
        var (branch, cat) = await Seed(ctx);
        var patron = await AddPatron(ctx, branch, "LC-0001");
        var waiter = await AddPatron(ctx, branch, "LC-0002");
        var item = new Item { Code = "BK-000001", Title = "Dune", Author = "Herbert", Isbn = "9780441172719",
            Category = cat, HomeBranch = branch, CurrentBranch = branch, Status = ItemStatus.Borrowed };
        ctx.Items.Add(item);
        var loan = new Loan { Item = item, Patron = patron, BorrowedAt = Clock.Now.AddDays(-1), DueAt = Clock.Today.AddDays(13) };
        ctx.Loans.Add(loan);
        ctx.Reservations.Add(new Reservation { Isbn = item.Isbn, TitleText = item.Title, Patron = waiter, PickupBranch = branch,
            PlacedAt = Clock.Now, QueueKey = Clock.Now, Status = HoldStatus.Waiting });
        await ctx.SaveChangesAsync();

        var svc = new ItemLifecycleService(ctx, new SettingsService(ctx),
            new NotificationService(ctx, new SettingsService(ctx), new FineService(ctx, new SettingsService(ctx)), NullLogger()),
            new FineService(ctx, new SettingsService(ctx)));
        var result = await svc.RenewAsync(loan.Id, patron.Id);

        Assert.False(result.Ok);
        Assert.Contains("waitlist", result.Message);
    }

    [Fact]
    public async Task Checkin_assigns_item_to_first_patron_in_queue()
    {
        using var ctx = _db.NewContext();
        var (branch, cat) = await Seed(ctx);
        var borrower = await AddPatron(ctx, branch, "LC-0003");
        var first = await AddPatron(ctx, branch, "LC-0004");
        var second = await AddPatron(ctx, branch, "LC-0005");
        var item = new Item { Code = "BK-000002", Title = "Sapiens", Author = "Harari", Isbn = "9780062316097",
            Category = cat, HomeBranch = branch, CurrentBranch = branch, Status = ItemStatus.Borrowed };
        ctx.Items.Add(item);
        ctx.Loans.Add(new Loan { Item = item, Patron = borrower, BorrowedAt = Clock.Now.AddDays(-5), DueAt = Clock.Today.AddDays(9) });
        ctx.Reservations.Add(new Reservation { Isbn = item.Isbn, TitleText = item.Title, Patron = first, PickupBranch = branch,
            PlacedAt = Clock.Now.AddMinutes(-10), QueueKey = Clock.Now.AddMinutes(-10), Status = HoldStatus.Waiting });
        ctx.Reservations.Add(new Reservation { Isbn = item.Isbn, TitleText = item.Title, Patron = second, PickupBranch = branch,
            PlacedAt = Clock.Now.AddMinutes(-5), QueueKey = Clock.Now.AddMinutes(-5), Status = HoldStatus.Waiting });
        await ctx.SaveChangesAsync();

        var settings = new SettingsService(ctx);
        var fines = new FineService(ctx, settings);
        var notify = new NotificationService(ctx, settings, fines, NullLogger());
        var svc = new ItemLifecycleService(ctx, settings, notify, fines);
        var result = await svc.CheckInAsync(item.Code, branch.Id, damaged: false, by: "test", deskId: null);

        Assert.True(result.Ok);
        var refreshed = await ctx.Items.FindAsync(item.Id);
        Assert.Equal(ItemStatus.Reserved, refreshed!.Status);
        var firstHold = await ctx.Reservations.FirstAsync(r => r.PatronId == first.Id);
        Assert.Equal(HoldStatus.Ready, firstHold.Status);
        var secondHold = await ctx.Reservations.FirstAsync(r => r.PatronId == second.Id);
        Assert.Equal(HoldStatus.Waiting, secondHold.Status);
    }

    static async Task<(Branch, Category)> Seed(AppDbContext ctx)
    {
        var branch = new Branch { Code = "CEN", Name = "Central Library" };
        var cat = new Category { Name = "Fiction" };
        ctx.Branches.Add(branch); ctx.Categories.Add(cat);
        foreach (var kv in SettingsService.Defaults) ctx.Settings.Add(new Setting { Key = kv.Key, Value = kv.Value });
        await ctx.SaveChangesAsync();
        return (branch, cat);
    }

    static async Task<AppUser> AddPatron(AppDbContext ctx, Branch branch, string card)
    {
        var u = new AppUser { Email = card + "@test.local", FullName = "Test " + card, Role = Role.Patron, HomeBranch = branch, CardNumber = card, PasswordHash = "x" };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u;
    }

    static Microsoft.Extensions.Logging.ILogger<NotificationService> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationService>.Instance;
}
