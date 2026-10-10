using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Reflection.Metadata;
using System.Text;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly LibraryDbContext _context;
        public ManagerController(LibraryDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var itemsForBranch = BranchItems();
            var loansForBranch = BranchBorrowRecords();
            var branchCode = Request.Cookies["LMS.BranchCode"] ?? "CEN";
            var selectedBranch = await _context.Branches
                .Where(b => b.Code == branchCode && b.IsActive)
                .Select(b => new { b.Id, b.Name, b.Code })
                .FirstOrDefaultAsync();
            var itemStatusCounts = await itemsForBranch
                .GroupBy(i => i.Status)
                .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalItems = itemStatusCounts.Sum(s => s.Count);
            var availableItems = itemStatusCounts.FirstOrDefault(s => s.Status == "Available")?.Count ?? 0;
            var borrowedItems = itemStatusCounts.FirstOrDefault(s => s.Status == "Borrowed")?.Count ?? 0;
            var categoryInventory = new List<CategoryInventorySummary>
            {
                await CategorySummaryAsync("Books", _context.Books.Where(i => i.Branch != null && i.Branch.Code == branchCode)),
                await CategorySummaryAsync("Music", _context.Music.Where(i => i.Branch != null && i.Branch.Code == branchCode)),
                await CategorySummaryAsync("Toys", _context.Toys.Where(i => i.Branch != null && i.Branch.Code == branchCode))
            };

            var totalBorrows = await loansForBranch.CountAsync();
            var activeBorrows = await loansForBranch.CountAsync(r => r.ReturnDate == null);
            var overdueBorrows = await loansForBranch.CountAsync(r => r.ReturnDate == null && r.DueDate < DateTime.Now);
            var returnedOnTime = await loansForBranch.CountAsync(r => r.ReturnDate != null && r.ReturnDate <= r.DueDate);

            var totalFinesIssued = await loansForBranch.Where(r => r.FineAmount > 0).SumAsync(r => (decimal?)r.FineAmount) ?? 0m;
            var collectedFines = await loansForBranch.Where(r => r.FineAmount > 0 && r.ReturnDate != null).SumAsync(r => (decimal?)r.FineAmount) ?? 0m;

            var overdueActiveRecords = await loansForBranch
                .Where(r => r.ReturnDate == null && r.DueDate < DateTime.Now)
                .ToListAsync();
            var outstandingFines = overdueActiveRecords
                .Sum(r => (decimal)(DateTime.Now - r.DueDate).Days * BorrowRecordController.DailyFineRate);

            var activeBranches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new BranchInventorySummary
                {
                    BranchId = b.Id,
                    BranchName = b.Name,
                    BranchCode = b.Code,
                    TotalItems = _context.Items.Count(i => i.BranchId == b.Id),
                    AvailableItems = _context.Items.Count(i => i.BranchId == b.Id && i.Status == "Available"),
                    BorrowedItems = _context.Items.Count(i => i.BranchId == b.Id && i.Status == "Borrowed"),
                    InTransitItems = _context.Items.Count(i => i.BranchId == b.Id && i.Status == "InTransit")
                })
                .ToListAsync();

            var trendStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            var borrowingByMonth = await loansForBranch
                .Where(r => r.BorrowDate >= trendStart)
                .GroupBy(r => new { r.BorrowDate.Year, r.BorrowDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();
            var borrowingTrend = Enumerable.Range(0, 6)
                .Select(offset => trendStart.AddMonths(offset))
                .Select(month =>
                {
                    var result = borrowingByMonth.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
                    return new MonthlyBorrowingSummary
                    {
                        Month = month.ToString("MMM"),
                        Count = result?.Count ?? 0
                    };
                })
                .ToList();

            var pendingTransferRequests = selectedBranch == null
                ? 0
                : await _context.TransferRequests.CountAsync(r => r.ToBranchId == selectedBranch.Id && r.Status == "Pending");

            return View(new ManagerDashboardViewModel
            {
                BranchName = selectedBranch?.Name ?? "All branches",
                BranchCode = selectedBranch?.Code ?? string.Empty,
                ItemStatusCounts = itemStatusCounts,
                TotalItems = totalItems,
                AvailableItems = availableItems,
                BorrowedItems = borrowedItems,
                TotalBorrows = totalBorrows,
                ActiveBorrows = activeBorrows,
                OverdueBorrows = overdueBorrows,
                ReturnedOnTime = returnedOnTime,
                TotalFinesIssued = totalFinesIssued,
                OutstandingFines = outstandingFines,
                CollectedFines = collectedFines,
                CategoryInventory = categoryInventory,
                BranchInventory = activeBranches,
                BorrowingTrend = borrowingTrend,
                PendingTransferRequests = pendingTransferRequests
            });
        }

        private static async Task<CategoryInventorySummary> CategorySummaryAsync(string category, IQueryable<Item> items)
        {
            var counts = await items
                .GroupBy(i => i.Status)
                .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            return new CategoryInventorySummary
            {
                Category = category,
                TotalItems = counts.Sum(c => c.Count),
                AvailableItems = counts.FirstOrDefault(c => c.Status == "Available")?.Count ?? 0,
                BorrowedItems = counts.FirstOrDefault(c => c.Status == "Borrowed")?.Count ?? 0
            };
        }

        // GET: Manager/ExportBorrowingCsv
        public async Task<IActionResult> ExportBorrowingCsv()
        {
            var records = await BranchBorrowRecords()
                .Include(r => r.Item).Include(r => r.Borrower)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Item,Borrower,BorrowDate,DueDate,ReturnDate,FineAmount");
            foreach (var r in records)
            {
                sb.AppendLine($"{r.Item?.Name},{r.Borrower?.FullName},{r.BorrowDate:yyyy-MM-dd},{r.DueDate:yyyy-MM-dd},{r.ReturnDate:yyyy-MM-dd},{r.FineAmount}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"borrowing-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        // GET: Manager/ExportFinesCsv
        public async Task<IActionResult> ExportFinesCsv()
        {
            var records = await BranchBorrowRecords()
                .Include(r => r.Item).Include(r => r.Borrower)
                .Where(r => r.FineAmount > 0)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Item,Borrower,DueDate,ReturnDate,FineAmount,Status");
            foreach (var r in records)
            {
                var status = r.ReturnDate == null ? "Outstanding" : "Collected";
                sb.AppendLine($"{r.Item?.Name},{r.Borrower?.FullName},{r.DueDate:yyyy-MM-dd},{r.ReturnDate:yyyy-MM-dd},{r.FineAmount},{status}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"fines-audit-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        public async Task<IActionResult> ExportBorrowingPdf()
        {
            var records = await BranchBorrowRecords().Include(r => r.Item).Include(r => r.Borrower).OrderByDescending(r => r.BorrowDate).ToListAsync();
            var bytes = BuildTablePdf("Borrowing Statistics Report", new[] { "Item", "Borrower", "Borrowed", "Due", "Returned", "Fine" },
                records.Select(r => new[] { r.Item?.Name ?? "-", r.Borrower?.FullName ?? "-", r.BorrowDate.ToString("yyyy-MM-dd"), r.DueDate.ToString("yyyy-MM-dd"), r.ReturnDate?.ToString("yyyy-MM-dd") ?? "Active", r.FineAmount.ToString("C") }).ToList());
            return File(bytes, "application/pdf", $"borrowing-report-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
        }

        public async Task<IActionResult> ExportFinesPdf()
        {
            var records = await BranchBorrowRecords().Include(r => r.Item).Include(r => r.Borrower).Where(r => r.FineAmount > 0).OrderByDescending(r => r.DueDate).ToListAsync();
            var bytes = BuildTablePdf("Fine Audit Report", new[] { "Item", "Borrower", "Due", "Returned", "Fine", "Status" },
                records.Select(r => new[] { r.Item?.Name ?? "-", r.Borrower?.FullName ?? "-", r.DueDate.ToString("yyyy-MM-dd"), r.ReturnDate?.ToString("yyyy-MM-dd") ?? "Outstanding", r.FineAmount.ToString("C"), r.ReturnDate == null ? "Outstanding" : "Collected" }).ToList());
            return File(bytes, "application/pdf", $"fines-audit-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
        }

        private IQueryable<Item> BranchItems()
        {
            var branchCode = Request.Cookies["LMS.BranchCode"] ?? "CEN";
            return _context.Items.Where(i => i.Branch != null && i.Branch.Code == branchCode);
        }

        private IQueryable<BorrowRecord> BranchBorrowRecords()
        {
            var branchCode = Request.Cookies["LMS.BranchCode"] ?? "CEN";
            return _context.BorrowRecords.Where(r => r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
        }

        private static byte[] BuildTablePdf(string title, string[] headers, List<string[]> rows)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.Header().Column(col => { col.Item().Text(title).FontSize(18).Bold(); col.Item().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(9); });
                    page.Content().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns => { foreach (var _ in headers) columns.RelativeColumn(); });
                        table.Header(header => { foreach (var h in headers) header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(h).Bold(); });
                        foreach (var row in rows) foreach (var cell in row) table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(cell).FontSize(8);
                    });
                    page.Footer().AlignRight().Text(text => { text.Span("Library Management System · Page "); text.CurrentPageNumber(); });
                });
            });
            return document.GeneratePdf();
        }

        // GET: Manager/ExportInventoryCsv
        public async Task<IActionResult> ExportInventoryCsv()
        {
            var items = await BranchItems().Include(i => i.Branch).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Name,LibraryCode,Status,Branch");
            foreach (var i in items)
            {
                sb.AppendLine($"{i.Name},{i.LibraryCode},{i.Status},{i.Branch?.Code}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"inventory-health-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        // GET: Manager/ExportInventoryPdf — example PDF export using QuestPDF.
        // Duplicate this pattern for ExportBorrowingPdf / ExportFinesPdf.
        public async Task<IActionResult> ExportInventoryPdf()
        {
            var items = await BranchItems().Include(i => i.Branch).ToListAsync();
            var generatedAt = DateTime.Now;

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.Header().Text("Inventory Health Report").FontSize(18).Bold();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Name").Bold();
                            header.Cell().Text("Library Code").Bold();
                            header.Cell().Text("Status").Bold();
                            header.Cell().Text("Branch").Bold();
                        });

                        foreach (var i in items)
                        {
                            table.Cell().Text(i.Name);
                            table.Cell().Text(i.LibraryCode);
                            table.Cell().Text(i.Status);
                            table.Cell().Text(i.Branch?.Code ?? "-");
                        }
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span($"Generated by {User.Identity?.Name} on {generatedAt:yyyy-MM-dd HH:mm} — page ");
                        txt.CurrentPageNumber();
                        txt.Span(" of ");
                        txt.TotalPages();
                    });
                });
            });

            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"inventory-health-{generatedAt:yyyyMMdd-HHmm}.pdf");
        }
    }

    public class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ManagerDashboardViewModel
    {
        public string BranchName { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public List<StatusCount> ItemStatusCounts { get; set; } = new();
        public int TotalItems { get; set; }
        public int AvailableItems { get; set; }
        public int BorrowedItems { get; set; }
        public int TotalBorrows { get; set; }
        public int ActiveBorrows { get; set; }
        public int OverdueBorrows { get; set; }
        public int ReturnedOnTime { get; set; }
        public decimal TotalFinesIssued { get; set; }
        public decimal OutstandingFines { get; set; }
        public decimal CollectedFines { get; set; }
        public List<CategoryInventorySummary> CategoryInventory { get; set; } = new();
        public List<BranchInventorySummary> BranchInventory { get; set; } = new();
        public List<MonthlyBorrowingSummary> BorrowingTrend { get; set; } = new();
        public int PendingTransferRequests { get; set; }
    }

    public class CategoryInventorySummary
    {
        public string Category { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int AvailableItems { get; set; }
        public int BorrowedItems { get; set; }
    }

    public class BranchInventorySummary
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int AvailableItems { get; set; }
        public int BorrowedItems { get; set; }
        public int InTransitItems { get; set; }
    }

    public class MonthlyBorrowingSummary
    {
        public string Month { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}