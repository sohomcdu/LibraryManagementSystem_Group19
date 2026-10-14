using System.Globalization;
using System.Text;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Services
{
    /// <summary>
    /// Loads the sample catalogue in docs/sample-data (books.csv, music.csv, toys.csv) when the
    /// app starts, so a fresh install shows the full catalogue without anyone importing by hand.
    ///
    /// Safe to run on every start-up: items that are already in the database are skipped
    /// (books by library code, music by title + artist, toys by title). If the folder or a file
    /// is missing, that file is simply skipped. New items can still be added with the CSV Importer.
    /// </summary>
    public static class SampleDataSeeder
    {
        public static async Task SeedAsync(LibraryDbContext db, string contentRootPath, ILogger logger)
        {
            var folder = Path.Combine(contentRootPath, "docs", "sample-data");
            if (!Directory.Exists(folder))
            {
                logger.LogInformation("Sample data folder not found ({Folder}); skipping sample catalogue.", folder);
                return;
            }

            var branchIds = await db.Branches.AsNoTracking()
                .ToDictionaryAsync(b => b.Code.ToUpperInvariant(), b => b.Id);
            var usedCodes = new HashSet<string>(
                await db.Items.AsNoTracking().Select(i => i.LibraryCode).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            int books = await SeedBooksAsync(db, Path.Combine(folder, "books.csv"), branchIds, usedCodes);
            int music = await SeedMusicAsync(db, Path.Combine(folder, "music.csv"), branchIds, usedCodes);
            int toys = await SeedToysAsync(db, Path.Combine(folder, "toys.csv"), branchIds, usedCodes);

            if (books + music + toys > 0)
            {
                await db.SaveChangesAsync();
            }
            logger.LogInformation("Sample catalogue: added {Books} books, {Music} music items and {Toys} toys.", books, music, toys);
        }

        // Books: one row per copy, each with its own LibraryCode (BK-1006 ... BK-1204).
        private static async Task<int> SeedBooksAsync(LibraryDbContext db, string path,
            Dictionary<string, int> branchIds, HashSet<string> usedCodes)
        {
            var rows = ReadCsv(path);
            if (rows == null) return 0;
            int added = 0;
            var next = NextNumber(usedCodes, "BK-", 1001);

            foreach (var r in rows)
            {
                var title = r.Get("Title");
                if (string.IsNullOrWhiteSpace(title) || !branchIds.TryGetValue(r.Get("BranchCode").ToUpperInvariant(), out var branchId))
                    continue;

                var code = r.Get("LibraryCode");
                if (!string.IsNullOrWhiteSpace(code) && usedCodes.Contains(code))
                    continue; // already loaded

                foreach (var _ in Enumerable.Range(0, string.IsNullOrWhiteSpace(code) ? r.Copies() : 1))
                {
                    var itemCode = string.IsNullOrWhiteSpace(code) ? NewCode("BK-", ref next, usedCodes) : code;
                    usedCodes.Add(itemCode);
                    db.Books.Add(new Book
                    {
                        Name = title,
                        Author = r.Get("Author"),
                        Isbn = new string(r.Get("ISBN").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(),
                        Genre = string.IsNullOrWhiteSpace(r.Get("Category")) ? "Uncategorised" : r.Get("Category"),
                        Description = r.Get("Description"),
                        LibraryCode = itemCode,
                        BranchId = branchId,
                        Status = "Available"
                    });
                    added++;
                }
            }
            return added;
        }

        // Music: skipped if an album with the same title and artist already exists.
        private static async Task<int> SeedMusicAsync(LibraryDbContext db, string path,
            Dictionary<string, int> branchIds, HashSet<string> usedCodes)
        {
            var rows = ReadCsv(path);
            if (rows == null) return 0;
            var existing = new HashSet<string>(
                (await db.Music.AsNoTracking().Select(m => new { m.Name, m.Artist }).ToListAsync()).Select(m => $"{m.Name}|{m.Artist}"),
                StringComparer.OrdinalIgnoreCase);
            int added = 0;
            var next = NextNumber(usedCodes, "MU-", 2001);

            foreach (var r in rows)
            {
                var title = r.Get("Title");
                var artist = r.Get("Artist");
                if (string.IsNullOrWhiteSpace(title) || !branchIds.TryGetValue(r.Get("BranchCode").ToUpperInvariant(), out var branchId))
                    continue;
                if (!existing.Add($"{title}|{artist}"))
                    continue; // already loaded

                int.TryParse(r.Get("ReleaseYear"), out var year);
                for (int c = 0; c < r.Copies(); c++)
                {
                    db.Music.Add(new Music
                    {
                        Name = title,
                        Artist = artist,
                        ReleaseYear = year,
                        Description = r.Get("Description"),
                        LibraryCode = NewCode("MU-", ref next, usedCodes),
                        BranchId = branchId,
                        Status = "Available"
                    });
                    added++;
                }
            }
            return added;
        }

        // Toys: skipped if a toy with the same title already exists.
        private static async Task<int> SeedToysAsync(LibraryDbContext db, string path,
            Dictionary<string, int> branchIds, HashSet<string> usedCodes)
        {
            var rows = ReadCsv(path);
            if (rows == null) return 0;
            var existing = new HashSet<string>(await db.Toys.AsNoTracking().Select(t => t.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase);
            int added = 0;
            var next = NextNumber(usedCodes, "TY-", 3001);

            foreach (var r in rows)
            {
                var title = r.Get("Title");
                if (string.IsNullOrWhiteSpace(title) || !branchIds.TryGetValue(r.Get("BranchCode").ToUpperInvariant(), out var branchId))
                    continue;
                if (!existing.Add(title))
                    continue; // already loaded

                int.TryParse(r.Get("MinimumAge"), out var age);
                for (int c = 0; c < r.Copies(); c++)
                {
                    db.Toys.Add(new Toy
                    {
                        Name = title,
                        Type = r.Get("Type"),
                        MinimumAge = age,
                        Description = r.Get("Description"),
                        LibraryCode = NewCode("TY-", ref next, usedCodes),
                        BranchId = branchId,
                        Status = "Available"
                    });
                    added++;
                }
            }
            return added;
        }

        // ---------- small CSV helpers ----------

        private sealed class Row
        {
            private readonly Dictionary<string, int> _header;
            private readonly List<string> _cells;
            public Row(Dictionary<string, int> header, List<string> cells) { _header = header; _cells = cells; }

            public string Get(string column) =>
                _header.TryGetValue(column.ToLowerInvariant(), out var i) && i < _cells.Count ? _cells[i].Trim() : string.Empty;

            public int Copies() => int.TryParse(Get("Copies"), out var n) && n is >= 1 and <= 20 ? n : 1;
        }

        private static List<Row>? ReadCsv(string path)
        {
            if (!File.Exists(path)) return null;
            var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (lines.Count < 2) return null;

            var header = ParseLine(lines[0])
                .Select((name, index) => (Name: name.Trim().TrimStart('﻿').ToLowerInvariant(), Index: index))
                .GroupBy(h => h.Name)
                .ToDictionary(g => g.Key, g => g.First().Index);
            return lines.Skip(1).Select(l => new Row(header, ParseLine(l))).ToList();
        }

        private static List<string> ParseLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (ch == '"') inQuotes = false;
                    else current.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(ch);
            }
            cells.Add(current.ToString());
            return cells;
        }

        private static int NextNumber(IEnumerable<string> codes, string prefix, int firstNumber)
        {
            var max = firstNumber - 1;
            foreach (var code in codes)
            {
                if (code != null && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(code.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                    && n > max)
                {
                    max = n;
                }
            }
            return max + 1;
        }

        private static string NewCode(string prefix, ref int next, HashSet<string> usedCodes)
        {
            string code;
            do { code = prefix + next++; } while (usedCodes.Contains(code));
            usedCodes.Add(code);
            return code;
        }
    }
}