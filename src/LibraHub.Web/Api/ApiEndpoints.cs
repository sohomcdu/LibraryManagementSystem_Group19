using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Api;

public record ItemDto(int Id, string Code, string Title, string Author, string Category, string Branch, string Status, string Isbn, int? Year);
public record AvailabilityDto(string Branch, int Copies, int Available, string? Status);

/// <summary>
/// Feature F2 – security pipeline for /api/v1/*: key check (401) → rate limit (429) → read-only guard (405).
/// Runs before routing so the same rules apply to every endpoint, including unknown ones.
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ApiKeyService keys, ApiRateLimiter limiter, AppDbContext db)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api/v1")) { await next(ctx); return; }

        static Task Problem(HttpContext c, int status, string title, string detail) =>
            Results.Problem(detail: detail, statusCode: status, title: title, type: "about:blank").ExecuteAsync(c);

        var plain = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(plain))
        { await Problem(ctx, 401, "Unauthorized", "Missing X-Api-Key header."); return; }

        var key = await keys.FindAsync(plain);
        if (key == null)
        { await Problem(ctx, 401, "Unauthorized", "Invalid API key."); return; }

        key.LastUsedAt = Clock.Now;
        if (!key.Active)
        {
            key.RejectedCount++; await db.SaveChangesAsync();   // "Revoked key → 401; usage recorded"
            await Problem(ctx, 401, "Unauthorized", "This API key has been revoked."); return;
        }

        var wait = limiter.TryHit(key.Id, Clock.Now);
        if (wait > 0)
        {
            ctx.Response.Headers.RetryAfter = wait.ToString();
            await Problem(ctx, 429, "Too Many Requests", $"Limit of {ApiRateLimiter.Limit} requests per minute exceeded."); return;
        }

        if (!HttpMethods.IsGet(ctx.Request.Method))
        {
            ctx.Response.Headers.Allow = "GET";
            await Problem(ctx, 405, "Method Not Allowed", "This API is read-only. Use GET."); return;
        }

        key.UsageCount++;
        await db.SaveChangesAsync();
        await next(ctx);
    }
}

public static class ApiEndpoints
{
    static ItemDto ToDto(Item i) => new(i.Id, i.Code, i.Title, i.Author, i.Category.Name, i.CurrentBranch.Code, Fmt.StatusText(i.Status), i.Isbn, i.Year);

    public static void MapLibraHubApi(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1");

        // GET /api/v1/items?q=&branch=CEN&category=&status=&page=1&pageSize=20
        g.MapGet("/items", async (AppDbContext db, string? q, string? branch, string? category, string? status, int? page, int? pageSize) =>
        {
            var size = Math.Clamp(pageSize ?? 20, 1, 100);   // default 20, max 100
            var no = Math.Max(1, page ?? 1);
            IQueryable<Item> query = db.Items.AsNoTracking().Include(i => i.Category).Include(i => i.CurrentBranch);
            foreach (var t in TextUtil.Normalize(q).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                query = query.Where(i => i.SearchText.Contains(t));
            if (!string.IsNullOrWhiteSpace(branch)) { var b = branch.ToUpperInvariant(); query = query.Where(i => i.CurrentBranch.Code == b); }
            if (!string.IsNullOrWhiteSpace(category)) { var c = category.ToLowerInvariant(); query = query.Where(i => i.Category.Name.ToLower() == c); }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var norm = status.Replace(" ", "").Replace("-", "");
                if (Enum.TryParse<ItemStatus>(norm, true, out var st)) query = query.Where(i => i.Status == st);
            }
            var total = await query.CountAsync();
            var rows = await query.OrderBy(i => i.Title).ThenBy(i => i.Code).Skip((no - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { page = no, pageSize = size, total, items = rows.Select(ToDto) });
        });

        // GET /api/v1/items/{id} – title-level view with availability per branch (spec 6.3)
        g.MapGet("/items/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.Items.AsNoTracking().Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
                return Results.Problem(detail: $"No item with id {id}.", statusCode: 404, title: "Not Found", type: "about:blank");
            var copies = await db.Items.AsNoTracking().Include(i => i.CurrentBranch).Where(i => i.Isbn == item.Isbn).ToListAsync();
            var avail = copies.GroupBy(c => c.CurrentBranch.Code).OrderBy(x => x.Key).Select(x =>
            {
                var a = x.Count(c => c.Status == ItemStatus.Available);
                return new AvailabilityDto(x.Key, x.Count(), a, a > 0 ? null : Fmt.StatusText(x.OrderBy(c => c.Status).First().Status));
            });
            return Results.Ok(new { item.Id, item.Code, item.Title, item.Author, category = item.Category.Name, item.Isbn, item.Year, availability = avail });
        });

        g.MapGet("/categories", async (AppDbContext db) =>
            Results.Ok(await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync()));

        g.MapGet("/status", async (AppDbContext db) =>
        {
            var now = Clock.Now;
            var branches = (await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync()).Select(b =>
            {
                var h = BranchHours.For(b, now);
                return new { code = b.Code, open = h.Open, closesAt = h.ClosesAt, opensAt = h.OpensAt };
            });
            return Results.Ok(new { generatedAt = new DateTimeOffset(now), branches, service = "Healthy" });
        });
    }
}
