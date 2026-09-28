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
            var itemStatusCounts = await _context.Items
                .GroupBy(i => i.Status)
                .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalItems = itemStatusCounts.Sum(s => s.Count);

            var totalBorrows = await _context.BorrowRecords.CountAsync();
            var activeBorrows = await _context.BorrowRecords.CountAsync(r => r.ReturnDate == null);
            var overdueBorrows = await _context.BorrowRecords.CountAsync(r => r.ReturnDate == null && r.DueDate < DateTime.Now);
            var returnedOnTime = await _context.BorrowRecords.CountAsync(r => r.ReturnDate != null && r.ReturnDate <= r.DueDate);

            var totalFinesIssued = await _context.BorrowRecords.Where(r => r.FineAmount > 0).SumAsync(r => r.FineAmount);
            var collectedFines = await _context.BorrowRecords.Where(r => r.FineAmount > 0 && r.ReturnDate != null).SumAsync(r => r.FineAmount);

            var overdueActiveRecords = await _context.BorrowRecords
                .Where(r => r.ReturnDate == null && r.DueDate < DateTime.Now)
                .ToListAsync();
            var outstandingFines = overdueActiveRecords
                .Sum(r => (decimal)(DateTime.Now - r.DueDate).Days * BorrowRecordController.DailyFineRate);

            return View(new ManagerDashboardViewModel
            {
                ItemStatusCounts = itemStatusCounts,
                TotalItems = totalItems,
                TotalBorrows = totalBorrows,
                ActiveBorrows = activeBorrows,
                OverdueBorrows = overdueBorrows,
                ReturnedOnTime = returnedOnTime,
                TotalFinesIssued = totalFinesIssued,
                OutstandingFines = outstandingFines,
                CollectedFines = collectedFines
            });
        }

        // GET: Manager/ExportBorrowingCsv
        public async Task<IActionResult> ExportBorrowingCsv()
        {
            var records = await _context.BorrowRecords
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
            var records = await _context.BorrowRecords
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

        // GET: Manager/ExportInventoryCsv
        public async Task<IActionResult> ExportInventoryCsv()
        {
            var items = await _context.Items.Include(i => i.Branch).ToListAsync();

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
            var items = await _context.Items.Include(i => i.Branch).ToListAsync();
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
        public List<StatusCount> ItemStatusCounts { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalBorrows { get; set; }
        public int ActiveBorrows { get; set; }
        public int OverdueBorrows { get; set; }
        public int ReturnedOnTime { get; set; }
        public decimal TotalFinesIssued { get; set; }
        public decimal OutstandingFines { get; set; }
        public decimal CollectedFines { get; set; }
    }
}