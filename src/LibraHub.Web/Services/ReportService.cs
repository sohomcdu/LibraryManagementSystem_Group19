using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

public class ReportFilter
{
    public DateTime From { get; set; } = Clock.Today.AddDays(-30);
    public DateTime To { get; set; } = Clock.Today;
    public int? BranchId { get; set; }
    public int? CategoryId { get; set; }
    public DateTime ToExclusive => To.Date.AddDays(1);
    public string Describe(IEnumerable<Branch> branches, IEnumerable<Category> cats) =>
        $"{From:d MMM yyyy} – {To:d MMM yyyy} · Branch: {(BranchId == null ? "All" : branches.FirstOrDefault(b => b.Id == BranchId)?.Name)} · Category: {(CategoryId == null ? "All" : cats.FirstOrDefault(c => c.Id == CategoryId)?.Name)}";
}

public record MonthPoint(string Label, int Count);
public record TopItem(string Title, int Loans);
public class BorrowingReport
{
    public int TotalLoans { get; set; }
    public int ActiveBorrowers { get; set; }
    public double AvgLoanDays { get; set; }
    public int OnTimePct { get; set; }
    public int PriorTotal { get; set; }
    public List<MonthPoint> PerMonth { get; set; } = new();
    public List<TopItem> Top { get; set; } = new();
}

public class FineLedgerRow
{
    public DateTime Date { get; set; }
    public string Patron { get; set; } = "";
    public string Item { get; set; } = "";
    public int DaysLate { get; set; }
    public int AmountCents { get; set; }
    public FineStatus Status { get; set; }
    public string Desk { get; set; } = "";
}
public class FineReport
{
    public int Issued, Collected, Outstanding, Waived;
    public bool Reconciled => Issued == Collected + Outstanding + Waived;
    public List<FineLedgerRow> Rows { get; set; } = new();
}

public record BranchHealth(string Branch, int Items, int Damaged, int Overdue, int Idle, bool Good, double DamagedPct, double IdlePct);
public class InventoryReport
{
    public Dictionary<ItemStatus, int> ByStatus { get; set; } = new();
    public List<BranchHealth> Branches { get; set; } = new();
    public List<Item> Damaged { get; set; } = new();
}

/// <summary>Feature F7 – one service feeds screen, CSV and PDF so the numbers always match.</summary>
public class ReportService(AppDbContext db, SettingsService settings)
{
    public IQueryable<Loan> LoanQuery(ReportFilter f)
    {
        var q = db.Loans.AsNoTracking().Include(l => l.Item).ThenInclude(i => i.Category).Include(l => l.Item.CurrentBranch)
            .Where(l => l.BorrowedAt >= f.From.Date && l.BorrowedAt < f.ToExclusive);
        if (f.BranchId != null) q = q.Where(l => l.Item.CurrentBranchId == f.BranchId);
        if (f.CategoryId != null) q = q.Where(l => l.Item.CategoryId == f.CategoryId);
        return q;
    }

    public async Task<BorrowingReport> BorrowingAsync(ReportFilter f)
    {
        var loans = await LoanQuery(f).ToListAsync();
        var r = new BorrowingReport { TotalLoans = loans.Count, ActiveBorrowers = loans.Select(l => l.PatronId).Distinct().Count() };
        var returned = loans.Where(l => l.ReturnedAt != null).ToList();
        r.AvgLoanDays = returned.Count == 0 ? 0 : Math.Round(returned.Average(l => (l.ReturnedAt!.Value - l.BorrowedAt).TotalDays), 1);
        r.OnTimePct = returned.Count == 0 ? 100 : (int)Math.Round(100.0 * returned.Count(l => l.ReturnedAt!.Value.Date <= l.DueAt.Date) / returned.Count);

        // prior period of the same length
        var span = (f.To.Date - f.From.Date).Days + 1;
        var pf = new ReportFilter { From = f.From.AddDays(-span), To = f.From.AddDays(-1), BranchId = f.BranchId, CategoryId = f.CategoryId };
        r.PriorTotal = await LoanQuery(pf).CountAsync();

        r.PerMonth = loans.GroupBy(l => new DateTime(l.BorrowedAt.Year, l.BorrowedAt.Month, 1)).OrderBy(g => g.Key)
            .Select(g => new MonthPoint(g.Key.ToString("MMM"), g.Count())).ToList();
        r.Top = loans.GroupBy(l => l.Item.Isbn).Select(g => new TopItem(g.First().Item.Title, g.Count()))
            .OrderByDescending(t => t.Loans).ThenBy(t => t.Title).Take(5).ToList();
        return r;
    }

    public async Task<FineReport> FinesAsync(ReportFilter f, bool showNames)
    {
        var q = db.Fines.AsNoTracking().Include(x => x.Patron).Include(x => x.Loan).ThenInclude(l => l.Item).Include(x => x.Desk)
            .Where(x => x.IssuedAt >= f.From.Date && x.IssuedAt < f.ToExclusive);
        if (f.BranchId != null) q = q.Where(x => x.Loan.Item.CurrentBranchId == f.BranchId);
        if (f.CategoryId != null) q = q.Where(x => x.Loan.Item.CategoryId == f.CategoryId);
        var fines = await q.OrderByDescending(x => x.IssuedAt).ToListAsync();
        var r = new FineReport
        {
            Issued = fines.Sum(x => x.AmountCents),
            Collected = fines.Where(x => x.Status == FineStatus.Collected).Sum(x => x.AmountCents),
            Outstanding = fines.Where(x => x.Status == FineStatus.Outstanding).Sum(x => x.AmountCents),
            Waived = fines.Where(x => x.Status == FineStatus.Waived).Sum(x => x.AmountCents)
        };
        r.Rows = fines.Select(x => new FineLedgerRow
        {
            Date = x.IssuedAt, Patron = showNames ? x.Patron.FullName : "(hidden)", Item = x.Loan.Item.Title,
            DaysLate = x.DaysLate, AmountCents = x.AmountCents, Status = x.Status, Desk = x.Desk?.Name ?? "—"
        }).ToList();
        return r;
    }

    public async Task<InventoryReport> InventoryAsync(ReportFilter f)
    {
        var s = await settings.LoadAsync();
        var items = await db.Items.AsNoTracking().Include(i => i.CurrentBranch).Include(i => i.Category)
            .Where(i => (f.BranchId == null || i.CurrentBranchId == f.BranchId) && (f.CategoryId == null || i.CategoryId == f.CategoryId)).ToListAsync();
        var since = Clock.Today.AddMonths(-12);
        var recent = (await db.Loans.AsNoTracking().Where(l => l.BorrowedAt >= since).Select(l => l.ItemId).Distinct().ToListAsync()).ToHashSet();
        var overdue = (await db.Loans.AsNoTracking().Where(l => l.ReturnedAt == null && l.DueAt < Clock.Today).Select(l => l.ItemId).ToListAsync()).ToHashSet();

        var r = new InventoryReport();
        foreach (ItemStatus st in Enum.GetValues<ItemStatus>()) r.ByStatus[st] = items.Count(i => i.Status == st);
        foreach (var g in items.GroupBy(i => i.CurrentBranch).OrderBy(g => g.Key.Id))
        {
            var n = g.Count();
            var dmg = g.Count(i => i.Status == ItemStatus.Damaged);
            var idle = g.Count(i => !recent.Contains(i.Id) && i.CreatedAt < since);
            var dp = n == 0 ? 0 : 100.0 * dmg / n; var ip = n == 0 ? 0 : 100.0 * idle / n;
            r.Branches.Add(new BranchHealth(g.Key.Name, n, dmg, g.Count(i => overdue.Contains(i.Id)), idle,
                dp < s.DamagedPct && ip < s.IdlePct, Math.Round(dp, 1), Math.Round(ip, 1)));
        }
        r.Damaged = items.Where(i => i.Status == ItemStatus.Damaged).OrderBy(i => i.Title).ToList();
        return r;
    }

    public async Task<HashSet<int>> RecentItemIdsAsync() =>
        (await db.Loans.AsNoTracking().Where(l => l.BorrowedAt >= Clock.Today.AddMonths(-12)).Select(l => l.ItemId).Distinct().ToListAsync()).ToHashSet();
}
