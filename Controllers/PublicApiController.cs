using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

/// <summary>Public, read-only endpoints for catalogue discovery and branch status.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public sealed class PublicApiController : ControllerBase
{
    private readonly LibraryDbContext _db;
    public PublicApiController(LibraryDbContext db) => _db = db;

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] string? q = null, [FromQuery] string? category = null, [FromQuery] string? type = null, [FromQuery] string? branch = null)
    {
        var books = _db.Books.AsNoTracking().Where(x => x.Status == "Available").Select(x => new { type = "Book", x.Id, x.LibraryCode, x.Name, category = x.Genre, x.Description, x.Status, x.BranchId });
        var music = _db.Music.AsNoTracking().Where(x => x.Status == "Available").Select(x => new { type = "Music", x.Id, x.LibraryCode, x.Name, category = "Music", x.Description, x.Status, x.BranchId });
        var toys = _db.Toys.AsNoTracking().Where(x => x.Status == "Available").Select(x => new { type = "Toy", x.Id, x.LibraryCode, x.Name, category = x.Type, x.Description, x.Status, x.BranchId });
        var all = await books.Concat(music).Concat(toys).ToListAsync();
        if (!string.IsNullOrWhiteSpace(type)) all = all.Where(x => x.type.Equals(type.Trim().TrimEnd('s'), StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(branch))
        {
            var branchId = await _db.Branches.AsNoTracking().Where(b => b.Code == branch && b.IsActive).Select(b => (int?)b.Id).FirstOrDefaultAsync();
            all = all.Where(x => branchId != null && x.BranchId == branchId).ToList();
        }
        if (!string.IsNullOrWhiteSpace(q)) all = all.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.LibraryCode.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(category)) all = all.Where(x => x.category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(new { count = all.Count, items = all.OrderBy(x => x.Name) });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories()
    {
        var books = await _db.Books.AsNoTracking().Select(x => x.Genre).Where(x => x != "").Distinct().ToListAsync();
        var toys = await _db.Toys.AsNoTracking().Select(x => x.Type).Where(x => x != "").Distinct().ToListAsync();
        var categories = books.Concat(toys).Append("Music").Distinct().OrderBy(x => x).ToArray();
        return Ok(new { count = categories.Length, categories });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var branches = await _db.Branches.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Code, x.Name, x.Address, isOpen = x.IsActive }).ToListAsync();
        return Ok(new { service = "Library Management System Public API", status = "operational", checkedAt = DateTimeOffset.UtcNow, branches });
    }
}
