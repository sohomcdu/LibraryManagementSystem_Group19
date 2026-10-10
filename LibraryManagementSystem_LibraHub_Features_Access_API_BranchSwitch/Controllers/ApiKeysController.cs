using System.Security.Cryptography;
using System.Text;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ApiKeysController : Controller
{
    private readonly LibraryDbContext _db;
    public ApiKeysController(LibraryDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string clientName)
    {
        clientName = (clientName ?? string.Empty).Trim();
        if (clientName.Length is < 2 or > 100)
        {
            TempData["Error"] = "Client name must be between 2 and 100 characters.";
            return RedirectToAction(nameof(Index));
        }

        var rawKey = "lms_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(36))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _db.ApiKeys.Add(new ApiKey { ClientName = clientName, KeyHash = Hash(rawKey), IsActive = true, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        TempData["NewApiKey"] = rawKey; // shown once; only a hash is persisted
        TempData["Success"] = "API key created. Copy it now; it will not be shown again.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return NotFound();
        key.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "API key revoked.";
        return RedirectToAction(nameof(Index));
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
