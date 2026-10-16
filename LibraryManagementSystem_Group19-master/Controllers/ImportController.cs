using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    /// <summary>
    /// Admin bulk import of catalogue items from CSV.
    ///
    /// The file type is detected from the header row, and columns are read by name (any order):
    ///   Books : Title, Author, ISBN, Category, BranchCode, Year, Copies, LibraryCode, Description
    ///   Music : Title, Artist, ReleaseYear, BranchCode, Copies, LibraryCode, Description
    ///   Toys  : Title, Type, MinimumAge, BranchCode, Copies, LibraryCode, Description
    ///
    /// The whole file is validated first and nothing is saved if any row has an error.
    /// Every copy gets its own library code; blank codes are generated using the site's
    /// numbering rule (BK-1xxx books, MU-2xxx music, TY-3xxx toys).
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ImportController : Controller
    {
        private const int MaxErrorsShown = 10;

        /// Toy types match the dropdown on the Toy create/edit pages.
        private static readonly string[] ToyTypes =
            { "Board Game", "Puzzle", "Building Blocks", "Action Figure", "Plush", "Educational", "Outdoor" };

        private readonly LibraryDbContext _context;
        public ImportController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var jobs = await _context.ImportJobs.OrderByDescending(j => j.PerformedAt).ToListAsync();
            return View(jobs);
        }

        /// GET: Import/Template?type=books|music|toys - a CSV with the right headers and one example row.
        public IActionResult Template(string type)
        {
            string content;
            switch ((type ?? "").ToLowerInvariant())
            {
                case "music":
                    content = "Title,Artist,ReleaseYear,BranchCode,Copies,LibraryCode,Description\r\n" +
                              "Example Album,Example Artist,2020,CEN,1,,A short description of the album.\r\n";
                    break;
                case "toys":
                    content = "Title,Type,MinimumAge,BranchCode,Copies,LibraryCode,Description\r\n" +
                              "Example Board Game,Board Game,8,NTH,1,,A short description of the toy.\r\n";
                    break;
                default:
                    type = "books";
                    content = "Title,Author,ISBN,Category,BranchCode,Year,Copies,LibraryCode,Description\r\n" +
                              "Example Book,Jordan Lee,9780306406157,Fiction,CEN,2024,1,,A short description of the book.\r\n";
                    break;
            }
            return File(Encoding.UTF8.GetBytes(content), "text/csv", $"import-template-{type}.csv");
        }

        // POST: Import/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file, string duplicatePolicy = "Skip")
        {
            if (file == null || file.Length == 0)
            {
                TempData["ImportError"] = "Please choose a CSV file.";
                return RedirectToAction(nameof(Index));
            }

            // "Update" was the old name for adding duplicates as extra copies.
            if (duplicatePolicy == "Update") duplicatePolicy = "Add";
            if (duplicatePolicy is not ("Skip" or "Add" or "Fail")) duplicatePolicy = "Skip";

            // ---------- read the file ----------
            var lines = new List<(int Number, List<string> Cells)>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                int number = 0;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    number++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    lines.Add((number, ParseCsvLine(line)));
                }
            }

            if (lines.Count == 0)
            {
                TempData["ImportError"] = "The file is empty.";
                return RedirectToAction(nameof(Index));
            }

            var header = lines[0].Cells.Select(h => h.Trim().TrimStart('﻿').ToLowerInvariant()).ToList();
            var kind = DetectKind(header);
            if (kind == null)
            {
                TempData["ImportError"] = "The header row was not recognised. A books file needs an ISBN column, a music file an Artist column, " +
                                          "and a toys file Type and MinimumAge columns. Download a template from this page to see the exact columns.";
                return RedirectToAction(nameof(Index));
            }

            int Col(string name) => header.IndexOf(name.ToLowerInvariant());
            string Cell(List<string> cells, string name)
            {
                var i = Col(name);
                return i >= 0 && i < cells.Count ? cells[i].Trim() : string.Empty;
            }

            // ---------- look-ups used during validation ----------
            var branches = await _context.Branches.AsNoTracking().Where(b => b.IsActive).ToListAsync();
            var branchByCode = branches.ToDictionary(b => b.Code.ToUpperInvariant(), b => b.Id);
            var existingCodes = new HashSet<string>(
                await _context.Items.AsNoTracking().Select(i => i.LibraryCode).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            var existingKeys = kind switch
            {
                "Book" => new HashSet<string>(await _context.Books.AsNoTracking().Where(b => b.Isbn != "").Select(b => b.Isbn).ToListAsync()),
                "Music" => new HashSet<string>((await _context.Music.AsNoTracking().Select(m => new { m.Name, m.Artist }).ToListAsync())
                                .Select(m => DuplicateKey(m.Name, m.Artist)), StringComparer.OrdinalIgnoreCase),
                _ => new HashSet<string>(await _context.Toys.AsNoTracking().Select(t => t.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase)
            };

            // ---------- validate every row ----------
            var errors = new List<string>();
            var toCreate = new List<(Item Item, int Copies)>();
            var codesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int skipped = 0;
            int thisYear = DateTime.Now.Year;

            foreach (var (n, cells) in lines.Skip(1))
            {
                var rowErrors = new List<string>();
                void Err(string message) => rowErrors.Add($"Row {n}: {message}");

                var title = Cell(cells, "Title");
                var branchCode = Cell(cells, "BranchCode").ToUpperInvariant();
                var copiesRaw = Cell(cells, "Copies");
                var libraryCode = Cell(cells, "LibraryCode");
                var description = Cell(cells, "Description");

                if (string.IsNullOrWhiteSpace(title) || title.Length > 200) Err("Title is required (max 200 characters).");
                if (description.Length > 1000) Err("Description must be 1000 characters or fewer.");

                if (string.IsNullOrWhiteSpace(branchCode)) Err("BranchCode is required (" + string.Join(", ", branchByCode.Keys) + ").");
                else if (!branchByCode.ContainsKey(branchCode)) Err($"Branch code '{branchCode}' does not exist.");

                int copies = 1;
                if (!string.IsNullOrWhiteSpace(copiesRaw) && (!int.TryParse(copiesRaw, out copies) || copies < 1 || copies > 20))
                {
                    Err("Copies must be between 1 and 20.");
                    copies = 1;
                }

                Item? item = null;
                string duplicateKey = string.Empty;

                if (kind == "Book")
                {
                    var author = Cell(cells, "Author");
                    var isbn = Regex.Replace(Cell(cells, "ISBN"), "[^0-9Xx]", "").ToUpperInvariant();
                    var category = Cell(cells, "Category");
                    var yearRaw = Cell(cells, "Year");

                    if (string.IsNullOrWhiteSpace(author) || author.Length > 150) Err("Author is required (max 150 characters).");
                    if (!Regex.IsMatch(isbn, @"^(\d{13}|\d{9}[\dX])$")) Err("ISBN must be 10 or 13 digits.");
                    if (!string.IsNullOrWhiteSpace(yearRaw) && (!int.TryParse(yearRaw, out var y) || y < 1000 || y > thisYear))
                        Err($"Year must be between 1000 and {thisYear}.");

                    duplicateKey = isbn;
                    item = new Book
                    {
                        Author = author,
                        Isbn = isbn,
                        Genre = string.IsNullOrWhiteSpace(category) ? "Uncategorised" : category
                    };
                }
                else if (kind == "Music")
                {
                    var artist = Cell(cells, "Artist");
                    var yearRaw = Cell(cells, "ReleaseYear");
                    int year = 0;

                    if (string.IsNullOrWhiteSpace(artist) || artist.Length > 150) Err("Artist is required (max 150 characters).");
                    if (!string.IsNullOrWhiteSpace(yearRaw) && (!int.TryParse(yearRaw, out year) || year < 1000 || year > thisYear))
                        Err($"ReleaseYear must be between 1000 and {thisYear}.");

                    duplicateKey = DuplicateKey(title, artist);
                    item = new Music { Artist = artist, ReleaseYear = year };
                }
                else
                {
                    var typeRaw = Cell(cells, "Type");
                    var ageRaw = Cell(cells, "MinimumAge");
                    var type = ToyTypes.FirstOrDefault(t => string.Equals(t, typeRaw, StringComparison.OrdinalIgnoreCase));
                    int age = 0;

                    if (type == null) Err("Type must be one of: " + string.Join(", ", ToyTypes) + ".");
                    if (!string.IsNullOrWhiteSpace(ageRaw) && (!int.TryParse(ageRaw, out age) || age < 0 || age > 18))
                        Err("MinimumAge must be between 0 and 18.");

                    duplicateKey = title;
                    item = new Toy { Type = type ?? string.Empty, MinimumAge = age };
                }

                // Duplicates are checked against what is already in the catalogue. Repeated rows
                // inside the same file are treated as extra copies, not duplicates.
                if (rowErrors.Count == 0 && existingKeys.Contains(duplicateKey))
                {
                    if (duplicatePolicy == "Skip") { skipped++; continue; }
                    if (duplicatePolicy == "Fail") Err($"'{title}' is already in the catalogue (duplicate policy is Fail).");
                }

                if (!string.IsNullOrWhiteSpace(libraryCode))
                {
                    if (copies > 1) Err("LibraryCode can only be given when Copies is 1, because every copy needs its own code. Leave it blank to generate codes.");
                    else if (libraryCode.Length > 50) Err("LibraryCode must be 50 characters or fewer.");
                    else if (existingCodes.Contains(libraryCode)) Err($"LibraryCode '{libraryCode}' is already used by another item.");
                    else if (!codesInFile.Add(libraryCode)) Err($"LibraryCode '{libraryCode}' appears more than once in this file.");
                }

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                    continue;
                }

                item!.Name = title;
                item.Description = description;
                item.LibraryCode = libraryCode;
                item.BranchId = branchByCode[branchCode];
                item.Status = "Available";
                toCreate.Add((item, copies));
            }

            var job = new ImportJob
            {
                Source = $"CSV · {(kind == "Book" ? "Books" : kind == "Music" ? "Music" : "Toys")} · {Path.GetFileName(file.FileName)}",
                RowCount = lines.Count - 1,
                ErrorCount = errors.Count,
                PerformedByEmail = User.Identity?.Name ?? "unknown",
                PerformedAt = DateTime.Now
            };

            if (errors.Any())
            {
                job.SuccessCount = 0;
                _context.ImportJobs.Add(job);
                await _context.SaveChangesAsync();

                TempData["ImportError"] = $"{errors.Count} error(s) found — nothing was saved. " +
                                          string.Join(" | ", errors.Take(MaxErrorsShown)) +
                                          (errors.Count > MaxErrorsShown ? $" | …and {errors.Count - MaxErrorsShown} more." : "");
                return RedirectToAction(nameof(Index));
            }

            // ---------- save: one Item per copy, each with its own code ----------
            var prefix = kind == "Book" ? "BK-" : kind == "Music" ? "MU-" : "TY-";
            var firstNumber = kind == "Book" ? 1001 : kind == "Music" ? 2001 : 3001;
            var nextNumber = NextCodeNumber(existingCodes.Concat(codesInFile), prefix, firstNumber);
            int saved = 0;

            foreach (var (template, copies) in toCreate)
            {
                for (int c = 0; c < copies; c++)
                {
                    var item = c == 0 ? template : CloneItem(template);
                    if (string.IsNullOrWhiteSpace(item.LibraryCode) || c > 0)
                    {
                        string code;
                        do { code = prefix + nextNumber++; } while (existingCodes.Contains(code) || codesInFile.Contains(code));
                        item.LibraryCode = code;
                    }
                    _context.Items.Add(item);
                    saved++;
                }
            }

            job.SuccessCount = saved;
            _context.ImportJobs.Add(job);
            await _context.SaveChangesAsync();

            var kindLabel = kind == "Book" ? "book" : kind == "Music" ? "music" : "toy";
            TempData["ImportSuccess"] = $"Imported {saved} {kindLabel} item(s) successfully." +
                                        (skipped > 0 ? $" {skipped} row(s) skipped because they were already in the catalogue." : "");
            return RedirectToAction(nameof(Index));
        }

        // ---------- helpers ----------

        /// Works out the file type from its header row.
        private static string? DetectKind(List<string> header)
        {
            if (!header.Contains("title")) return null;
            if (header.Contains("isbn")) return "Book";
            if (header.Contains("artist")) return "Music";
            if (header.Contains("type") && header.Contains("minimumage")) return "Toy";
            return null;
        }

        private static string DuplicateKey(string title, string artist) => $"{title.Trim()}|{artist.Trim()}";

        /// Next free number for a prefix, e.g. BK-1205 when BK-1204 is the highest existing book code.
        private static int NextCodeNumber(IEnumerable<string> codes, string prefix, int firstNumber)
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

        private static Item CloneItem(Item source)
        {
            Item copy = source switch
            {
                Book b => new Book { Author = b.Author, Isbn = b.Isbn, Genre = b.Genre },
                Music m => new Music { Artist = m.Artist, ReleaseYear = m.ReleaseYear },
                Toy t => new Toy { Type = t.Type, MinimumAge = t.MinimumAge },
                _ => throw new InvalidOperationException("Unknown item type.")
            };
            copy.Name = source.Name;
            copy.Description = source.Description;
            copy.BranchId = source.BranchId;
            copy.Status = source.Status;
            return copy;
        }

        /// Splits one CSV line, supporting "quoted, values" and doubled "" quotes inside quotes.
        private static List<string> ParseCsvLine(string line)
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
    }
}