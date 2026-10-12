using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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

        [HttpGet]
        public async Task<IActionResult> Workload(DateOnly? fromDate, DateOnly? toDate)
        {
            var report = await BuildWorkloadReportAsync(fromDate, toDate);
            if (report == null)
            {
                ModelState.AddModelError(string.Empty, "The start date must be on or before the end date.");
                var today = DateOnly.FromDateTime(DateTime.Today);
                report = new ManagerWorkloadViewModel
                {
                    FromDate = fromDate ?? today.AddDays(-29),
                    ToDate = toDate ?? today,
                    BranchName = await CurrentBranchNameAsync()
                };
            }

            return View(report);
        }

        [HttpGet]
        public async Task<IActionResult> ExportWorkloadCsv(DateOnly? fromDate, DateOnly? toDate)
        {
            var report = await BuildWorkloadReportAsync(fromDate, toDate);
            if (report == null)
            {
                return BadRequest("The start date must be on or before the end date.");
            }

            var csv = new StringBuilder();
            csv.AppendLine("Manager workload report");
            csv.AppendLine($"Branch,{EscapeCsvValue(report.BranchName)}");
            csv.AppendLine($"From,{report.FromDate:yyyy-MM-dd}");
            csv.AppendLine($"To,{report.ToDate:yyyy-MM-dd}");
            csv.AppendLine();
            csv.AppendLine("Activity,Total,Unresolved");

            foreach (var activity in report.Activity)
            {
                csv.AppendLine($"{EscapeCsvValue(activity.Activity)},{activity.Total},{activity.Unresolved}");
            }

            csv.AppendLine();
            csv.AppendLine("Backlog type,Reference,Description,Status,Created or due date,Age in days");
            foreach (var item in report.Backlog)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsvValue(item.Type),
                    EscapeCsvValue(item.Reference),
                    EscapeCsvValue(item.Description),
                    EscapeCsvValue(item.Status),
                    item.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    item.AgeDays.ToString(CultureInfo.InvariantCulture)));
            }

            var fileName = $"manager-workload-{report.FromDate:yyyyMMdd}-{report.ToDate:yyyyMMdd}.csv";
            var fileContents = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());

            return File(fileContents, "text/csv; charset=utf-8", fileName);
        }

        private async Task<ManagerWorkloadViewModel?> BuildWorkloadReportAsync(DateOnly? fromDate, DateOnly? toDate)
        {
            // Use a half-open interval so the selected end date includes all of that day's records.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = fromDate ?? today.AddDays(-29);
            var endDate = toDate ?? today;
            if (startDate > endDate)
            {
                return null;
            }

            var rangeStart = startDate.ToDateTime(TimeOnly.MinValue);
            var rangeEnd = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var branchCode = CurrentBranchCode();
            var branch = await _context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Code == branchCode && b.IsActive);

            var loans = BranchBorrowRecords();
            var borrowRequests = _context.BorrowRequests
                .Where(r => r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
            var reservations = _context.Reservations
                .Where(r => r.Item != null && r.Item.Branch != null && r.Item.Branch.Code == branchCode);
            var transferRequests = branch == null
                ? _context.TransferRequests.Where(_ => false)
                : _context.TransferRequests.Where(r => r.FromBranchId == branch.Id || r.ToBranchId == branch.Id);
            var transfers = branch == null
                ? _context.ItemTransfers.Where(_ => false)
                : _context.ItemTransfers.Where(t => t.FromBranchId == branch.Id || t.ToBranchId == branch.Id);

            var activity = new List<WorkloadActivitySummary>
            {
                new()
                {
                    Activity = "Checkouts",
                    Total = await loans.CountAsync(r => r.BorrowDate >= rangeStart && r.BorrowDate < rangeEnd),
                    Unresolved = await loans.CountAsync(r => r.BorrowDate >= rangeStart && r.BorrowDate < rangeEnd && r.ReturnDate == null)
                },
                new()
                {
                    Activity = "Returns",
                    Total = await loans.CountAsync(r => r.ReturnDate >= rangeStart && r.ReturnDate < rangeEnd),
                    Unresolved = 0
                },
                new()
                {
                    Activity = "Borrow requests",
                    Total = await borrowRequests.CountAsync(r => r.RequestDate >= rangeStart && r.RequestDate < rangeEnd),
                    Unresolved = await borrowRequests.CountAsync(r => r.RequestDate >= rangeStart && r.RequestDate < rangeEnd && r.Status == "Pending")
                },
                new()
                {
                    Activity = "Reservations",
                    Total = await reservations.CountAsync(r => r.RequestedDate >= rangeStart && r.RequestedDate < rangeEnd),
                    Unresolved = await reservations.CountAsync(r => r.RequestedDate >= rangeStart && r.RequestedDate < rangeEnd && (r.Status == "Waiting" || r.Status == "Ready"))
                },
                new()
                {
                    Activity = "Transfer requests",
                    Total = await transferRequests.CountAsync(r => r.RequestedAt >= rangeStart && r.RequestedAt < rangeEnd),
                    Unresolved = await transferRequests.CountAsync(r => r.RequestedAt >= rangeStart && r.RequestedAt < rangeEnd && r.Status == "Pending")
                },
                new()
                {
                    Activity = "Item transfers",
                    Total = await transfers.CountAsync(t => t.InitiatedDate >= rangeStart && t.InitiatedDate < rangeEnd),
                    Unresolved = await transfers.CountAsync(t => t.InitiatedDate >= rangeStart && t.InitiatedDate < rangeEnd && t.Status == "InTransit")
                }
            };

            var backlog = await GetBacklogAsync(
                loans, borrowRequests, reservations, transferRequests, transfers, rangeStart, rangeEnd);

            return new ManagerWorkloadViewModel
            {
                FromDate = startDate,
                ToDate = endDate,
                BranchName = branch?.Name ?? "Selected branch unavailable",
                Activity = activity,
                Backlog = backlog
            };
        }

        private async Task<List<WorkloadBacklogItem>> GetBacklogAsync(
            IQueryable<BorrowRecord> loans,
            IQueryable<BorrowRequest> borrowRequests,
            IQueryable<Reservation> reservations,
            IQueryable<TransferRequest> transferRequests,
            IQueryable<ItemTransfer> transfers,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var backlog = new List<WorkloadBacklogItem>();

            var pendingBorrowRequests = await borrowRequests.AsNoTracking()
                .Include(request => request.Item)
                .Include(request => request.Borrower)
                .Where(request => request.Status == "Pending"
                    && request.RequestDate >= rangeStart
                    && request.RequestDate < rangeEnd)
                .ToListAsync();

            backlog.AddRange(pendingBorrowRequests.Select(request => new WorkloadBacklogItem
            {
                Type = "Borrow request",
                Reference = $"BR-{request.Id}",
                Description = $"{request.Item?.Name ?? "Item unavailable"} · {request.Borrower?.FullName ?? "Patron unavailable"}",
                Status = request.Status,
                CreatedAt = request.RequestDate,
                AgeDays = AgeInDays(request.RequestDate)
            }));

            var activeReservations = await reservations.AsNoTracking()
                .Include(reservation => reservation.Item)
                .Include(reservation => reservation.Borrower)
                .Where(reservation => (reservation.Status == "Waiting" || reservation.Status == "Ready")
                    && reservation.RequestedDate >= rangeStart
                    && reservation.RequestedDate < rangeEnd)
                .ToListAsync();

            backlog.AddRange(activeReservations.Select(reservation => new WorkloadBacklogItem
            {
                Type = "Reservation",
                Reference = $"H-{reservation.Id}",
                Description = $"{reservation.Item?.Name ?? "Item unavailable"} · {reservation.Borrower?.FullName ?? "Patron unavailable"}",
                Status = reservation.Status,
                CreatedAt = reservation.RequestedDate,
                AgeDays = AgeInDays(reservation.RequestedDate)
            }));

            var pendingTransferRequests = await transferRequests.AsNoTracking()
                .Include(request => request.FromBranch)
                .Include(request => request.ToBranch)
                .Where(request => request.Status == "Pending"
                    && request.RequestedAt >= rangeStart
                    && request.RequestedAt < rangeEnd)
                .ToListAsync();

            backlog.AddRange(pendingTransferRequests.Select(request => new WorkloadBacklogItem
            {
                Type = "Transfer request",
                Reference = $"TR-{request.Id}",
                Description = $"{request.Quantity} {request.ItemCategory} item(s) · {request.FromBranch?.Code} to {request.ToBranch?.Code}",
                Status = request.Status,
                CreatedAt = request.RequestedAt,
                AgeDays = AgeInDays(request.RequestedAt)
            }));

            var transfersInTransit = await transfers.AsNoTracking()
                .Include(transfer => transfer.Item)
                .Include(transfer => transfer.FromBranch)
                .Include(transfer => transfer.ToBranch)
                .Where(transfer => transfer.Status == "InTransit"
                    && transfer.InitiatedDate >= rangeStart
                    && transfer.InitiatedDate < rangeEnd)
                .ToListAsync();

            backlog.AddRange(transfersInTransit.Select(transfer => new WorkloadBacklogItem
            {
                Type = "Item in transit",
                Reference = $"T-{transfer.Id}",
                Description = $"{transfer.Item?.Name ?? "Item unavailable"} · {transfer.FromBranch?.Code} to {transfer.ToBranch?.Code}",
                Status = transfer.Status,
                CreatedAt = transfer.InitiatedDate,
                AgeDays = AgeInDays(transfer.InitiatedDate)
            }));

            var overdueLoans = await loans.AsNoTracking()
                .Include(loan => loan.Item)
                .Include(loan => loan.Borrower)
                .Where(loan => loan.ReturnDate == null
                    && loan.DueDate < DateTime.Today
                    && loan.DueDate >= rangeStart
                    && loan.DueDate < rangeEnd)
                .ToListAsync();

            backlog.AddRange(overdueLoans.Select(loan => new WorkloadBacklogItem
            {
                Type = "Overdue loan",
                Reference = $"L-{loan.Id}",
                Description = $"{loan.Item?.Name ?? "Item unavailable"} · {loan.Borrower?.FullName ?? "Patron unavailable"}",
                Status = "Overdue",
                CreatedAt = loan.DueDate,
                AgeDays = AgeInDays(loan.DueDate)
            }));

            return backlog
                .OrderByDescending(item => item.AgeDays)
                .ThenBy(item => item.CreatedAt)
                .ToList();
        }

        private string CurrentBranchCode() => Request.Cookies["LMS.BranchCode"] ?? "CEN";

        private async Task<string> CurrentBranchNameAsync() => await _context.Branches.AsNoTracking()
            .Where(branch => branch.Code == CurrentBranchCode() && branch.IsActive)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync() ?? "Selected branch unavailable";

        private static int AgeInDays(DateTime date) => Math.Max(0, (DateTime.Today - date.Date).Days);

        private static string EscapeCsvValue(string? value)
        {
            value ??= string.Empty;

            // Prefix spreadsheet formula characters so exported text stays text when opened in Excel.
            var trimmedValue = value.TrimStart();
            if (trimmedValue.StartsWith('=') || trimmedValue.StartsWith('+')
                || trimmedValue.StartsWith('-') || trimmedValue.StartsWith('@'))
            {
                value = "'" + value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
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
