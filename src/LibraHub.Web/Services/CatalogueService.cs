using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

public class CatalogueQuery
{
    public string? Q { get; set; }
    public int? Category { get; set; }              // top-bar drop-down
    public int? Branch { get; set; }
    public string? Availability { get; set; }        // any | available | borrowed | damaged
    public List<int> Cats { get; set; } = new();     // refine-panel check-boxes
    public List<int> Brs { get; set; } = new();
    public List<string> Avs { get; set; } = new();
    public string Sort { get; set; } = "relevance";
    public int PageNo { get; set; } = 1;
}

public record TitleRow(string Isbn, string Title, string Author, string Category, string CoverColors,
    int FirstItemId, List<Item> Copies, int Queue);

public record Facet(int Id, string Name, int Count);

public class CatalogueResult
{
    public int Total { get; set; }
    public int PageNo { get; set; }
    public int PageSize { get; set; } = 20;
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public List<TitleRow> Rows { get; set; } = new();
    public List<Facet> CategoryFacets { get; set; } = new();
    public List<Facet> BranchFacets { get; set; } = new();
    public Dictionary<string, int> AvailFacets { get; set; } = new();
}

public record BranchAvailability(Branch Branch, int Copies, int Available, ItemStatus Status, string Note);

public class DetailsModel
{
    public Item Item { get; set; } = null!;
    public List<BranchAvailability> Branches { get; set; } = new();
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int Queue { get; set; }
    public string EstimatedWait { get; set; } = "";
    public List<Item> Similar { get; set; } = new();
}

/// <summary>Feature F4/F5 read side: catalogue search grouped by ISBN with per-branch availability.</summary>
public class CatalogueService(AppDbContext db, SettingsService settings)
{
    static string PrepareTerm(string t) =>
        System.Text.RegularExpressions.Regex.IsMatch(t, @"^[\d\-xX]{10,17}$") ? Isbn.Clean(t).ToLowerInvariant() : t;

    static IQueryable<Item> Keyword(IQueryable<Item> q, string? text)
    {
        foreach (var raw in TextUtil.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var term = PrepareTerm(raw);
            q = q.Where(i => i.SearchText.Contains(term)); // partial words, case- & accent-insensitive
        }
        return q;
    }

    static List<ItemStatus> StatusesFor(IEnumerable<string> keys) =>
        keys.Select(k => k.ToLowerInvariant() switch
        {
            "available" => (ItemStatus?)ItemStatus.Available,
            "borrowed" => ItemStatus.Borrowed,
            "damaged" => ItemStatus.Damaged,
            _ => null
        }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();

    public async Task<CatalogueResult> SearchAsync(CatalogueQuery cq)
    {
        var res = new CatalogueResult { PageNo = Math.Max(1, cq.PageNo) };
        var kq = Keyword(db.Items.AsNoTracking(), cq.Q);
        var q = kq;

        if (cq.Category != null) q = q.Where(i => i.CategoryId == cq.Category);
        if (cq.Cats.Count > 0) q = q.Where(i => cq.Cats.Contains(i.CategoryId));
        if (cq.Branch != null) q = q.Where(i => i.CurrentBranchId == cq.Branch);
        if (cq.Brs.Count > 0) q = q.Where(i => cq.Brs.Contains(i.CurrentBranchId));
        var statuses = StatusesFor(cq.Avs);
        if (!string.IsNullOrEmpty(cq.Availability) && cq.Availability != "any")
            statuses.AddRange(StatusesFor(new[] { cq.Availability }));
        if (statuses.Count > 0) q = q.Where(i => statuses.Contains(i.Status));

        var nq = TextUtil.Normalize(cq.Q);
        var groups = q.GroupBy(i => i.Isbn).Select(g => new
        {
            Isbn = g.Key,
            Title = g.Min(x => x.Title),
            Avail = g.Count(x => x.Status == ItemStatus.Available),
            Year = g.Max(x => x.Year),
            Newest = g.Max(x => x.Id),
            TitleHit = g.Count(x => x.Title.ToLower().Contains(nq))
        });

        res.Total = await groups.CountAsync();
        var ordered = cq.Sort switch
        {
            "title" => groups.OrderBy(g => g.Title),
            "newest" => groups.OrderByDescending(g => g.Year).ThenByDescending(g => g.Newest),
            "availability" => groups.OrderByDescending(g => g.Avail).ThenBy(g => g.Title),
            _ => groups.OrderByDescending(g => g.TitleHit).ThenBy(g => g.Title)
        };
        var page = await ordered.Skip((res.PageNo - 1) * res.PageSize).Take(res.PageSize).ToListAsync();
        var isbns = page.Select(p => p.Isbn).ToList();

        var copies = await db.Items.AsNoTracking().Include(i => i.CurrentBranch).Include(i => i.Category)
            .Where(i => isbns.Contains(i.Isbn)).OrderBy(i => i.CurrentBranchId).ThenBy(i => i.Id).ToListAsync();
        var queues = (await db.Reservations.Where(r => isbns.Contains(r.Isbn) && r.Status == HoldStatus.Waiting)
            .GroupBy(r => r.Isbn).Select(g => new { g.Key, C = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.C);

        foreach (var p in page)
        {
            var c = copies.Where(x => x.Isbn == p.Isbn).ToList();
            var first = c.First();
            res.Rows.Add(new TitleRow(p.Isbn, first.Title, first.Author, first.Category.Name, first.CoverColors,
                first.Id, c, queues.GetValueOrDefault(p.Isbn)));
        }

        // facets are computed from the keyword only, so the counts stay useful while filtering
        var cats = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
        res.CategoryFacets = (await kq.GroupBy(i => i.CategoryId)
                .Select(g => new { g.Key, C = g.Select(x => x.Isbn).Distinct().Count() }).ToListAsync())
            .Select(x => new Facet(x.Key, cats.GetValueOrDefault(x.Key, "?"), x.C)).OrderByDescending(f => f.Count).ToList();
        var brs = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        res.BranchFacets = (await kq.GroupBy(i => i.CurrentBranchId)
                .Select(g => new { g.Key, C = g.Select(x => x.Isbn).Distinct().Count() }).ToListAsync())
            .Select(x => new Facet(x.Key, brs.GetValueOrDefault(x.Key, "?").Replace(" Library", "").Replace(" Branch", ""), x.C))
            .OrderBy(f => f.Name).ToList();
        foreach (var (key, st) in new[] { ("available", ItemStatus.Available), ("borrowed", ItemStatus.Borrowed), ("damaged", ItemStatus.Damaged) })
            res.AvailFacets[key] = await kq.Where(i => i.Status == st).Select(i => i.Isbn).Distinct().CountAsync();
        return res;
    }

    public async Task<DetailsModel?> DetailsAsync(int itemId)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null) return null;
        var s = await settings.LoadAsync();
        var copies = await db.Items.AsNoTracking().Include(i => i.CurrentBranch)
            .Where(i => i.Isbn == item.Isbn).ToListAsync();
        var ids = copies.Select(c => c.Id).ToList();
        var loans = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.ItemId) && l.ReturnedAt == null).ToListAsync();
        var transfers = await db.Transfers.AsNoTracking().Include(t => t.ToBranch)
            .Where(t => ids.Contains(t.ItemId) && t.Status == TransferStatus.Dispatched).ToListAsync();

        var m = new DetailsModel { Item = item, TotalCopies = copies.Count };
        foreach (var g in copies.GroupBy(c => c.CurrentBranch).OrderBy(g => g.Key.Id))
        {
            var avail = g.Count(c => c.Status == ItemStatus.Available);
            // representative status: Available wins, otherwise the first unavailable copy
            var rep = avail > 0 ? g.First(c => c.Status == ItemStatus.Available) : g.OrderBy(c => c.Status).First();
            var due = loans.Where(l => g.Any(c => c.Id == l.ItemId)).Select(l => (DateTime?)l.DueAt).Min();
            var transfer = transfers.FirstOrDefault(t => t.ItemId == rep.Id);
            string note = rep.Status switch
            {
                ItemStatus.Available => "On the shelf",
                ItemStatus.Borrowed => due != null ? $"Next due back {Fmt.Day(due.Value)}" : "On loan",
                ItemStatus.InTransit => transfer != null
                    ? $"Moving to {transfer.ToBranch.Name.Replace(" Library", "").Replace(" Branch", "")} · ETA {Fmt.Day((transfer.DispatchedAt ?? Clock.Now).AddDays(2))}"
                    : "Moving between branches",
                ItemStatus.Damaged => rep.StatusNote ?? "In repair",
                _ => "Held for a patron"
            };
            m.Branches.Add(new BranchAvailability(g.Key, g.Count(), avail, rep.Status, note));
            m.AvailableCopies += avail;
        }

        m.Queue = await db.Reservations.CountAsync(r => r.Isbn == item.Isbn && r.Status == HoldStatus.Waiting);
        var days = s.LoanDays * (m.Queue + 1.0) / Math.Max(1, m.TotalCopies); // avg loan length × queue ÷ copies
        var lo = Math.Max(1, (int)Math.Floor(days / 7)); var hi = Math.Max(lo, (int)Math.Ceiling(days / 7));
        m.EstimatedWait = lo == hi ? $"about {lo} week{(lo > 1 ? "s" : "")}" : $"{lo}–{hi} weeks";

        var candidates = await db.Items.AsNoTracking()
            .Where(i => i.Isbn != item.Isbn && (i.CategoryId == item.CategoryId || i.Author == item.Author))
            .OrderBy(i => i.Id).Take(60).ToListAsync();
        m.Similar = candidates.GroupBy(i => i.Isbn).Select(g => g.First()).Take(4).ToList();
        return m;
    }
}
