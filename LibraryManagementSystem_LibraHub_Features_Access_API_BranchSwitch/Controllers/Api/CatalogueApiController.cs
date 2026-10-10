using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers.Api;

/// <summary>Public, read-only endpoints for catalogue integrations.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1")]
public sealed class CatalogueApiController : ControllerBase
{
    private readonly LibraryDbContext _db;
    public CatalogueApiController(LibraryDbContext db) => _db = db;

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] string? q, [FromQuery] string? category, [FromQuery] string? branch)
    {
        var query = _db.Items.AsNoTracking().Include(i => i.Branch).Where(i => i.Status == "Available");
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i => i.Name.Contains(term) || i.LibraryCode.Contains(term) || i.Description.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(branch)) query = query.Where(i => i.Branch != null && i.Branch.Code == branch);
        var rows = await query.OrderBy(i => i.Name).ToListAsync();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var kind = category.Trim().ToLowerInvariant();
            rows = rows.Where(i => kind is "book" or "books" ? i is Book : kind is "music" or "album" ? i is Music : kind is "toy" or "toys" ? i is Toy : true).ToList();
        }
        return Ok(rows.Select(i => new { i.Id, i.Name, i.LibraryCode, i.Description, i.Status, category = i switch { Book => "Book", Music => "Music", Toy => "Toy", _ => "Other" }, branch = i.Branch?.Name, branchCode = i.Branch?.Code }));
    }

    [HttpGet("categories")]
    public IActionResult Categories() => Ok(new[] { new { name = "Books", slug = "book" }, new { name = "Music", slug = "music" }, new { name = "Toys", slug = "toy" } });

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var branches = await _db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name)
            .Select(b => new { b.Code, b.Name, b.Address, b.IsActive }).ToListAsync();
        return Ok(new { service = "Library Management System Public API", status = "operational", checkedAt = DateTimeOffset.UtcNow, branches });
    }
}
