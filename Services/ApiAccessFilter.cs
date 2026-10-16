using LibraryManagementSystem.Controllers;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Services
{
    /// <summary>
    /// Security and logging for the REST API, applied to CatalogueApiController.
    ///
    /// - /api/v1/*     : requires a valid, active X-API-Key header, otherwise HTTP 401.
    /// - /api/public/* : open to anyone (read-only discovery), no key needed.
    ///
    /// Every call, allowed or rejected, is written to ApiRequestLogs so the Admin key page
    /// and the Manager API usage report can show real usage.
    /// </summary>
    public class ApiAccessFilter : IAsyncActionFilter
    {
        public const string HeaderName = "X-API-Key";

        private readonly LibraryDbContext _db;

        public ApiAccessFilter(LibraryDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var requiresKey = request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);
            var log = new ApiRequestLog
            {
                RequestedAt = DateTime.UtcNow,
                Channel = requiresKey ? "Key" : "Public",
                Path = Truncate(request.Path.Value ?? string.Empty, 200),
                SearchTerm = Truncate(request.Query["q"].FirstOrDefault()?.Trim(), 100)
            };
            if (string.IsNullOrEmpty(log.SearchTerm)) log.SearchTerm = null;

            ApiKey? key = null;
            if (requiresKey)
            {
                var supplied = request.Headers[HeaderName].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(supplied))
                {
                    context.Result = Problem(401, "Unauthorized", $"Missing {HeaderName} header. Ask a library Admin for an API key.");
                    await SaveLogAsync(log, 401);
                    return;
                }

                var hash = ApiKeysController.Hash(supplied.Trim());
                key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
                if (key == null || !key.IsActive)
                {
                    // A revoked key is still linked to its client so the failed call shows in reports.
                    log.ApiKeyId = key?.Id;
                    var reason = key == null ? "Invalid API key." : "This API key has been revoked.";
                    context.Result = Problem(401, "Unauthorized", reason);
                    await SaveLogAsync(log, 401);
                    return;
                }

                log.ApiKeyId = key.Id;
            }

            var executed = await next();

            var status = executed.Exception != null && !executed.ExceptionHandled
                ? 500
                : (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;

            if (key != null && status < 400)
            {
                key.LastUsedAt = DateTime.UtcNow;
                key.RequestCount++;
            }

            await SaveLogAsync(log, status);
        }

        private async Task SaveLogAsync(ApiRequestLog log, int status)
        {
            log.StatusCode = status;
            _db.ApiRequestLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        private static ObjectResult Problem(int status, string title, string detail) =>
            new(new ProblemDetails { Status = status, Title = title, Detail = detail })
            {
                StatusCode = status,
                ContentTypes = { "application/problem+json" }
            };

        private static string? Truncate(string? value, int max) =>
            value == null ? null : value.Length <= max ? value : value[..max];
    }
}
