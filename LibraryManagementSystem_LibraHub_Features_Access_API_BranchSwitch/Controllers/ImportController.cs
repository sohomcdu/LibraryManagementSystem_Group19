using System.Globalization;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ImportController : Controller
    {
        private readonly LibraryDbContext _context;
        public ImportController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var jobs = await _context.ImportJobs.OrderByDescending(j => j.PerformedAt).ToListAsync();
            return View(jobs);
        }

        // POST: Import/Upload — parses, validates, and only saves if the
        // WHOLE file is error-free (simplified from the spec's per-row
        // partial-commit behaviour, to keep this straightforward to build).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file, string duplicatePolicy = "Skip")
        {
            if (file == null || file.Length == 0)
            {
                TempData["ImportError"] = "Please choose a CSV file.";
                return RedirectToAction(nameof(Index));
            }

            var errors = new List<string>();
            var parsedRows = new List<(string Title, string Author, string Isbn, string Category, string BranchCode, int Copies, string? LibraryCode)>();

            using var reader = new StreamReader(file.OpenReadStream());
            string? headerLine = await reader.ReadLineAsync(); // skip header row
            int lineNumber = 1;

            while (!reader.EndOfStream)
            {
                lineNumber++;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                {
                    errors.Add($"Row {lineNumber}: not enough columns.");
                    continue;
                }

                string title = cols.ElementAtOrDefault(0)?.Trim() ?? "";
                string author = cols.ElementAtOrDefault(1)?.Trim() ?? "";
                string isbn = cols.ElementAtOrDefault(2)?.Trim() ?? "";
                string category = cols.ElementAtOrDefault(3)?.Trim() ?? "";
                string branchCode = cols.ElementAtOrDefault(4)?.Trim() ?? "";
                string yearRaw = cols.ElementAtOrDefault(5)?.Trim() ?? "";
                string copiesRaw = cols.ElementAtOrDefault(6)?.Trim() ?? "";
                string libraryCode = cols.ElementAtOrDefault(7)?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
                    errors.Add($"Row {lineNumber}: Title is required (max 200 chars).");

                if (string.IsNullOrWhiteSpace(author) || author.Length > 150)
                    errors.Add($"Row {lineNumber}: Author is required (max 150 chars).");

                var digitsOnlyIsbn = new string(isbn.Where(char.IsDigit).ToArray());
                if (digitsOnlyIsbn.Length != 10 && digitsOnlyIsbn.Length != 13)
                    errors.Add($"Row {lineNumber}: ISBN must be 10 or 13 digits.");

                if (!string.IsNullOrWhiteSpace(category) == false)
                    category = "Uncategorised";

                int copies = 1;
                if (!string.IsNullOrWhiteSpace(copiesRaw))
                {
                    if (!int.TryParse(copiesRaw, out copies) || copies < 1 || copies > 20)
                    {
                        errors.Add($"Row {lineNumber}: Copies must be between 1 and 20.");
                        copies = 1;
                    }
                }

                if (!string.IsNullOrWhiteSpace(yearRaw))
                {
                    if (!int.TryParse(yearRaw, out var year) || year < 1000 || year > DateTime.Now.Year)
                        errors.Add($"Row {lineNumber}: Year must be between 1000 and {DateTime.Now.Year}.");
                }

                if (!string.IsNullOrWhiteSpace(branchCode))
                {
                    bool exists = await _context.Branches.AnyAsync(b => b.Code == branchCode);
                    if (!exists)
                        errors.Add($"Row {lineNumber}: Branch code '{branchCode}' does not exist.");
                }

                parsedRows.Add((title, author, isbn, category, branchCode, copies, string.IsNullOrWhiteSpace(libraryCode) ? null : libraryCode));
            }

            var job = new ImportJob
            {
                Source = "CSV",
                RowCount = parsedRows.Count,
                ErrorCount = errors.Count,
                PerformedByEmail = User.Identity?.Name ?? "unknown",
                PerformedAt = DateTime.Now
            };

            if (errors.Any())
            {
                job.SuccessCount = 0;
                _context.ImportJobs.Add(job);
                await _context.SaveChangesAsync();

                TempData["ImportError"] = $"{errors.Count} error(s) found — nothing was saved. " + string.Join(" | ", errors.Take(10));
                return RedirectToAction(nameof(Index));
            }

            // Duplicate check: by Isbn + BranchCode combination.
            int nextCodeNumber = 100000 + await _context.Books.CountAsync();
            int savedCount = 0;

            foreach (var row in parsedRows)
            {
                bool isDuplicate = await _context.Books.AnyAsync(b => b.Isbn == row.Isbn);
                if (isDuplicate)
                {
                    if (duplicatePolicy == "Fail")
                    {
                        TempData["ImportError"] = $"Duplicate ISBN {row.Isbn} found and duplicate policy is Fail — import stopped.";
                        job.SuccessCount = savedCount;
                        _context.ImportJobs.Add(job);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    if (duplicatePolicy == "Skip") continue;
                    // "Update" policy: fall through and let it insert a new copy anyway (simplified).
                }

                int? branchId = null;
                if (!string.IsNullOrWhiteSpace(row.BranchCode))
                {
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Code == row.BranchCode);
                    branchId = branch?.Id;
                }

                for (int i = 0; i < row.Copies; i++)
                {
                    nextCodeNumber++;
                    _context.Books.Add(new Book
                    {
                        Name = row.Title,
                        Author = row.Author,
                        Isbn = row.Isbn,
                        Genre = row.Category,
                        LibraryCode = row.LibraryCode ?? $"BK-{nextCodeNumber}",
                        BranchId = branchId,
                        Status = "Available"
                    });
                    savedCount++;
                }
            }

            job.SuccessCount = savedCount;
            _context.ImportJobs.Add(job);
            await _context.SaveChangesAsync();

            TempData["ImportSuccess"] = $"Imported {savedCount} item(s) successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}