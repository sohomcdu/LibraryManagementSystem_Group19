using System.Globalization;
using System.Text;
using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    /// <summary>
    /// Manager report on how the REST API is being used: request volume, which
    /// integrations call it, rejected (401) calls and what people search for.
    /// Data comes from ApiRequestLogs, written by Services/ApiAccessFilter.cs.
    /// </summary>
    [Authorize(Roles = "Manager,Admin")]
    public class ApiUsageController : Controller
    {
        private static readonly int[] AllowedPeriods = { 7, 30, 90 };
        private readonly LibraryDbContext _context;

        public ApiUsageController(LibraryDbContext context)
        {
            _context = context;
        }

        /// GET: ApiUsage?days=30
        public async Task<IActionResult> Index(int days = 30)
        {
            return View(await BuildAsync(days));
        }

        /// GET: ApiUsage/ExportCsv?days=30 - every logged request in the period.
        public async Task<IActionResult> ExportCsv(int days = 30)
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            var since = DateTime.UtcNow.AddDays(-days);

            var keyNames = await _context.ApiKeys.AsNoTracking().ToDictionaryAsync(k => k.Id, k => k.ClientName);
            var rows = await _context.ApiRequestLogs.AsNoTracking()
                .Where(l => l.RequestedAt >= since)
                .OrderBy(l => l.RequestedAt)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("RequestedAt,Channel,Client,Path,SearchTerm,StatusCode");
            foreach (var r in rows)
            {
                var client = r.ApiKeyId.HasValue && keyNames.TryGetValue(r.ApiKeyId.Value, out var name) ? name : (r.Channel == "Public" ? "Public" : "Unknown key");
                csv.AppendLine(string.Join(",",
                    r.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    r.Channel,
                    Csv(client),
                    Csv(r.Path),
                    Csv(r.SearchTerm),
                    r.StatusCode.ToString(CultureInfo.InvariantCulture)));
            }

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"api-usage-last-{days}-days.csv");
        }

        private async Task<ApiUsageViewModel> BuildAsync(int days)
        {
            if (!AllowedPeriods.Contains(days)) days = 30;
            var since = DateTime.UtcNow.AddDays(-days);
            var logs = _context.ApiRequestLogs.AsNoTracking().Where(l => l.RequestedAt >= since);

            var vm = new ApiUsageViewModel
            {
                Days = days,
                TotalRequests = await logs.CountAsync(),
                KeyedRequests = await logs.CountAsync(l => l.Channel == "Key"),
                PublicRequests = await logs.CountAsync(l => l.Channel == "Public"),
                RejectedRequests = await logs.CountAsync(l => l.StatusCode == 401),
                ActiveKeys = await _context.ApiKeys.CountAsync(k => k.IsActive)
            };

            // Requests per day for the last 14 days (or fewer for the 7-day view).
            var chartDays = Math.Min(days, 14);
            var chartStart = DateTime.Today.AddDays(-(chartDays - 1));
            var chartStartUtc = chartStart.ToUniversalTime();
            var recent = await logs.Where(l => l.RequestedAt >= chartStartUtc).Select(l => l.RequestedAt).ToListAsync();
            vm.PerDay = Enumerable.Range(0, chartDays)
                .Select(i => chartStart.AddDays(i))
                .Select(d => new ApiDayCount
                {
                    Day = d,
                    Count = recent.Count(t => t.ToLocalTime().Date == d)
                })
                .ToList();

            // Usage per integration (API key).
            var perKey = await logs.Where(l => l.ApiKeyId != null)
                .GroupBy(l => l.ApiKeyId)
                .Select(g => new { KeyId = g.Key, Requests = g.Count(), Failed = g.Count(l => l.StatusCode >= 400) })
                .ToListAsync();
            var keys = await _context.ApiKeys.AsNoTracking().ToListAsync();
            vm.Clients = keys
                .Select(k => new ApiClientUsage
                {
                    ClientName = k.ClientName,
                    IsActive = k.IsActive,
                    LastUsedAt = k.LastUsedAt,
                    Requests = perKey.FirstOrDefault(p => p.KeyId == k.Id)?.Requests ?? 0,
                    Failed = perKey.FirstOrDefault(p => p.KeyId == k.Id)?.Failed ?? 0
                })
                .OrderByDescending(c => c.Requests)
                .ThenBy(c => c.ClientName)
                .ToList();

            vm.TopEndpoints = await logs
                .GroupBy(l => l.Path)
                .Select(g => new ApiNameCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToListAsync();

            vm.TopSearches = await logs.Where(l => l.SearchTerm != null)
                .GroupBy(l => l.SearchTerm!)
                .Select(g => new ApiNameCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToListAsync();

            return vm;
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;
            var trimmed = value.TrimStart();
            if (trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('-') || trimmed.StartsWith('@'))
            {
                value = "'" + value;
            }
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }

    public class ApiUsageViewModel
    {
        public int Days { get; set; }
        public int TotalRequests { get; set; }
        public int KeyedRequests { get; set; }
        public int PublicRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int ActiveKeys { get; set; }
        public List<ApiDayCount> PerDay { get; set; } = new();
        public List<ApiClientUsage> Clients { get; set; } = new();
        public List<ApiNameCount> TopEndpoints { get; set; } = new();
        public List<ApiNameCount> TopSearches { get; set; } = new();
    }

    public class ApiDayCount
    {
        public DateTime Day { get; set; }
        public int Count { get; set; }
    }

    public class ApiClientUsage
    {
        public string ClientName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public int Requests { get; set; }
        public int Failed { get; set; }
    }

    public class ApiNameCount
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
