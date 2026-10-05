using System.Text.Json;
using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

public enum RowState { Ok, Warning, Error }
public enum DuplicatePolicy { Skip, Update, Fail }

public class ImportRow
{
    public int Line { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Isbn { get; set; } = "";
    public string Category { get; set; } = "Uncategorised";
    public string BranchCode { get; set; } = "";
    public int? Year { get; set; }
    public int Copies { get; set; } = 1;
    public string? LibraryCode { get; set; }
    public RowState State { get; set; } = RowState.Ok;
    public List<string> Messages { get; set; } = new();
    public bool Duplicate { get; set; }
    public bool WillImport => State != RowState.Error && !(Duplicate && Policy == DuplicatePolicy.Skip);
    public DuplicatePolicy Policy { get; set; }
    public string[] Raw { get; set; } = Array.Empty<string>();
}

public class ImportPreview
{
    public string? FileError { get; set; }
    public List<ImportRow> Rows { get; set; } = new();
    public int Valid => Rows.Count(r => r.State == RowState.Ok);
    public int Warnings => Rows.Count(r => r.State == RowState.Warning);
    public int Errors => Rows.Count(r => r.State == RowState.Error);
}

// ---- F6 metadata provider abstraction (mockup 15 #2) -----------------------------------------------
public record ProviderRecord(string Isbn, string Title, string Author, string? Publisher, int? Year, int? Pages,
    string? Description, string[] Subjects, string? Language);

public interface IMetadataProvider
{
    string Name { get; }
    Task<IReadOnlyList<ProviderRecord>> SearchAsync(string query);
}

/// <summary>Demo provider – reads SampleData/provider-catalogue.json; makes NO internet call.</summary>
public class LocalJsonMetadataProvider(IWebHostEnvironment env) : IMetadataProvider
{
    public string Name => "Local demo catalogue (JSON)";
    public async Task<IReadOnlyList<ProviderRecord>> SearchAsync(string query)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SampleData", "provider-catalogue.json");
        if (!File.Exists(path)) path = Path.Combine(env.ContentRootPath, "SampleData", "provider-catalogue.json");
        var all = JsonSerializer.Deserialize<List<ProviderRecord>>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        var nq = TextUtil.Normalize(query); var isbn = Isbn.Clean(query);
        return all.Where(r => string.IsNullOrWhiteSpace(query)
                || TextUtil.Normalize(r.Title + " " + r.Author).Contains(nq)
                || (isbn.Length >= 5 && r.Isbn.Contains(isbn))).ToList();
    }
}

/// <summary>Feature F6 – CSV validate/preview/commit and provider import. Nothing is saved before confirmation.</summary>
public class ImportService(AppDbContext db, ItemLifecycleService lifecycle)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public static readonly string[] Header = { "Title", "Author", "Isbn", "Category", "BranchCode", "Year", "Copies", "LibraryCode" };

    public const string Template =
        "Title,Author,Isbn,Category,BranchCode,Year,Copies,LibraryCode\r\n" +
        "Sapiens,Yuval Noah Harari,9780062316097,History,CEN,2014,2,\r\n" +
        "Educated,Tara Westover,9780399590504,Memoir,NTH,2018,1,\r\n";

    public async Task<ImportPreview> ValidateAsync(string csvText, int defaultBranchId, DuplicatePolicy policy)
    {
        var pv = new ImportPreview();
        List<List<string>> rows;
        try { rows = CsvUtil.Parse(new StringReader(csvText)); }
        catch (Exception ex) { pv.FileError = "Could not read the file: " + ex.Message; return pv; }
        if (rows.Count == 0) { pv.FileError = "The file is empty."; return pv; }

        var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        foreach (var need in new[] { "title", "author", "isbn" })
            if (!head.Contains(need)) { pv.FileError = $"Missing required column “{need}”. Download the template for the expected columns."; return pv; }
        string Cell(List<string> r, string col) { var i = head.IndexOf(col); return i >= 0 && i < r.Count ? r[i].Trim() : ""; }

        var branches = await db.Branches.AsNoTracking().ToListAsync();
        var defCode = branches.First(b => b.Id == defaultBranchId).Code;
        var cats = (await db.Categories.AsNoTracking().ToListAsync()).ToDictionary(c => c.Name.ToLowerInvariant(), c => c.Name);
        var existing = (await db.Items.AsNoTracking().Select(i => new { i.Isbn, Branch = i.HomeBranch.Code }).ToListAsync())
            .Select(x => $"{x.Isbn}|{x.Branch}").ToHashSet();
        var existingCodes = (await db.Items.AsNoTracking().Select(i => i.Code).ToListAsync()).ToHashSet();
        var seenInFile = new HashSet<string>(); var codesInFile = new HashSet<string>();

        for (int n = 1; n < rows.Count; n++)
        {
            var r = rows[n];
            var row = new ImportRow { Line = n + 1, Policy = policy, Raw = r.ToArray(),
                Title = Cell(r, "title"), Author = Cell(r, "author"), BranchCode = Cell(r, "branchcode").ToUpperInvariant() };
            void Err(string m) { row.State = RowState.Error; row.Messages.Add(m); }
            void Warn(string m) { if (row.State == RowState.Ok) row.State = RowState.Warning; row.Messages.Add(m); }

            if (row.Title.Length is < 1 or > 200) Err("Title is required (1–200 characters).");
            if (row.Author.Length is < 1 or > 150) Err("Author is required (1–150 characters).");
            var rawIsbn = Cell(r, "isbn");
            if (!Isbn.IsValid(rawIsbn)) Err($"Invalid ISBN “{rawIsbn}” (check digit failed).");
            else row.Isbn = Isbn.ToIsbn13(rawIsbn);

            var cat = Cell(r, "category");
            if (cat.Length == 0) { Warn("No category – set to Uncategorised."); row.Category = "Uncategorised"; }
            else if (cats.TryGetValue(cat.ToLowerInvariant(), out var cn)) row.Category = cn;
            else { Warn($"Unknown category “{cat}” – set to Uncategorised."); row.Category = "Uncategorised"; }

            if (row.BranchCode.Length == 0) row.BranchCode = defCode;
            else if (!branches.Any(b => b.Code == row.BranchCode)) Err($"Unknown branch code “{row.BranchCode}” (use {string.Join(", ", branches.Select(b => b.Code))}).");

            var y = Cell(r, "year");
            if (y.Length > 0)
            {
                if (int.TryParse(y, out var yy) && yy >= 1000 && yy <= Clock.Today.Year) row.Year = yy;
                else Err($"Year must be between 1000 and {Clock.Today.Year}.");
            }
            var c = Cell(r, "copies");
            if (c.Length > 0)
            {
                if (int.TryParse(c, out var cc) && cc is >= 1 and <= 20) row.Copies = cc;
                else Err("Copies must be 1–20.");
            }
            var lc = Cell(r, "librarycode");
            if (lc.Length > 0)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(lc, @"^BK-\d{6}$")) Err("LibraryCode must look like BK-100234.");
                else if (existingCodes.Contains(lc) || !codesInFile.Add(lc)) Err($"LibraryCode {lc} already exists.");
                else if (row.Copies > 1) Err("LibraryCode can only be used with Copies = 1.");
                else row.LibraryCode = lc;
            }

            if (row.State != RowState.Error && row.Isbn.Length > 0)
            {
                var key = $"{row.Isbn}|{row.BranchCode}";
                if (existing.Contains(key) || !seenInFile.Add(key))
                {
                    row.Duplicate = true;
                    switch (policy)
                    {
                        case DuplicatePolicy.Fail: Err("Duplicate ISBN + branch (policy: Fail)."); break;
                        case DuplicatePolicy.Skip: Warn("Duplicate ISBN + branch – will be skipped."); break;
                        default: Warn("Duplicate ISBN + branch – existing copies will be updated."); break;
                    }
                }
            }
            pv.Rows.Add(row);
        }
        return pv;
    }

    async Task<int> NextCodeNumberAsync()
    {
        var codes = await db.Items.Where(i => i.Code.StartsWith("BK-")).Select(i => i.Code).ToListAsync();
        var max = codes.Select(c => int.TryParse(c.AsSpan(3), out var n) ? n : 0).DefaultIfEmpty(100000).Max();
        return Math.Max(max, 100000) + 1;
    }

    public static string SearchTextFor(Item i, string category) =>
        TextUtil.Normalize($"{i.Title} {i.Subtitle} {i.Author} {i.Isbn} {category} {i.Publisher} {i.Code}");

    static readonly string[] Palette = { "#12324A", "#0F766E", "#7C2D12", "#4C1D95", "#14532D", "#7F1D1D", "#1E3A8A", "#78350F" };
    public static string CoverFor(string seed)
    {
        var h = Math.Abs(seed.Aggregate(17, (a, c) => a * 31 + c));
        return $"{Palette[h % Palette.Length]},{Palette[(h / 7 + 3) % Palette.Length]}";
    }

    /// <summary>Commit valid rows – all or nothing (single SaveChanges = single transaction).</summary>
    public async Task<ImportJob> CommitAsync(ImportPreview pv, string source, string userName)
    {
        var branches = await db.Branches.ToDictionaryAsync(b => b.Code);
        var cats = await db.Categories.ToDictionaryAsync(c => c.Name);
        var next = await NextCodeNumberAsync();
        int imported = 0, skipped = 0;

        foreach (var row in pv.Rows)
        {
            if (row.State == RowState.Error) continue;
            if (row.Duplicate && row.Policy == DuplicatePolicy.Skip) { skipped++; continue; }
            var branch = branches[row.BranchCode];
            var cat = cats.TryGetValue(row.Category, out var cc) ? cc : cats["Uncategorised"];

            if (row.Duplicate && row.Policy == DuplicatePolicy.Update)
            {
                var same = await db.Items.Where(i => i.Isbn == row.Isbn && i.HomeBranchId == branch.Id).ToListAsync();
                foreach (var i in same)
                {
                    i.Title = row.Title; i.Author = row.Author; i.CategoryId = cat.Id; i.Year = row.Year ?? i.Year;
                    i.SearchText = SearchTextFor(i, cat.Name); i.Version++;
                    db.ItemEvents.Add(new ItemEvent { Item = i, Text = "Updated by import", By = userName, At = Clock.Now });
                }
                imported++; continue;
            }

            for (int k = 0; k < row.Copies; k++)
            {
                var item = new Item
                {
                    Code = row.LibraryCode ?? $"BK-{next++:D6}", Title = row.Title, Author = row.Author, Isbn = row.Isbn,
                    CategoryId = cat.Id, Year = row.Year, HomeBranchId = branch.Id, CurrentBranchId = branch.Id,
                    CoverColors = CoverFor(row.Title), Status = ItemStatus.Available, CreatedAt = Clock.Now
                };
                item.SearchText = SearchTextFor(item, cat.Name);
                db.Items.Add(item);
                await lifecycle.OnItemAddedAsync(item, userName);
            }
            imported++;
        }
        var job = new ImportJob { Source = source, UserName = userName, At = Clock.Now, TotalRows = pv.Rows.Count,
            Imported = imported, Skipped = skipped, Errors = pv.Errors };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    /// <summary>Provider import: creates the chosen copies for each selected record.</summary>
    public async Task<ImportJob> ImportProviderAsync(IEnumerable<(ProviderRecord Rec, int CategoryId)> picks,
        int branchId, int copies, string providerName, string userName)
    {
        var next = await NextCodeNumberAsync();
        int imported = 0, skipped = 0, total = 0;
        var cats = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name);
        foreach (var (rec, catId) in picks)
        {
            total++;
            if (await db.Items.AnyAsync(i => i.Isbn == rec.Isbn && i.HomeBranchId == branchId)) { skipped++; continue; }
            for (int k = 0; k < copies; k++)
            {
                var item = new Item
                {
                    Code = $"BK-{next++:D6}", Title = rec.Title, Author = rec.Author, Isbn = rec.Isbn, CategoryId = catId,
                    Publisher = rec.Publisher, Year = rec.Year, Pages = rec.Pages, Description = rec.Description,
                    Language = rec.Language ?? "English", HomeBranchId = branchId, CurrentBranchId = branchId,
                    CoverColors = CoverFor(rec.Title), CreatedAt = Clock.Now
                };
                item.SearchText = SearchTextFor(item, cats[catId]);
                db.Items.Add(item);
                await lifecycle.OnItemAddedAsync(item, userName);
            }
            imported++;
        }
        var job = new ImportJob { Source = providerName, UserName = userName, At = Clock.Now, TotalRows = total, Imported = imported, Skipped = skipped };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }
}
