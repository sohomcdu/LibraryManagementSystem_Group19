using System.Security.Claims;
using LibraHub.Data;
using LibraHub.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace LibraHub.Infrastructure;

public class AuthService
{
    public const int MaxFailures = 5;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;

    public AuthService(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public string Hash(AppUser u, string password) => _hasher.HashPassword(u, password);

    public async Task<(AppUser? User, string? Error)> ValidatePasswordAsync(string email, string password)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Email == e);
        if (u == null) return (null, "Invalid email or password.");
        return await CheckAsync(u, u.PasswordHash, password, "Invalid email or password.");
    }

    public async Task<(AppUser? User, string? Error)> ValidateCardPinAsync(string card, string pin)
    {
        var c = LendingService.NormalizeCard(card);
        var u = await _db.Users.FirstOrDefaultAsync(x => x.CardNumber == c && x.Role == Role.Patron);
        if (u == null || u.PinHash == null) return (null, "Unknown library card or PIN.");
        return await CheckAsync(u, u.PinHash, pin ?? "", "Unknown library card or PIN.");
    }

    async Task<(AppUser?, string?)> CheckAsync(AppUser u, string hash, string secret, string generic)
    {
        if (u.LockoutEnd > Clock.Now)
            return (null, $"Too many failed attempts. Try again in {Math.Ceiling((u.LockoutEnd.Value - Clock.Now).TotalMinutes)} minute(s).");
        if (_hasher.VerifyHashedPassword(u, hash, secret) == PasswordVerificationResult.Failed)
        {
            u.FailedLogins++;
            if (u.FailedLogins >= MaxFailures) { u.LockoutEnd = Clock.Now.AddMinutes(5); u.FailedLogins = 0; }
            await _db.SaveChangesAsync();
            return (null, generic);
        }
        u.FailedLogins = 0; u.LockoutEnd = null;
        await _db.SaveChangesAsync();
        return (u, null);
    }

    public async Task SignInAsync(HttpContext ctx, AppUser u, bool persistent)
    {
        var branchId = u.HomeBranchId;
        if (u.DeskId != null && branchId == null)
            branchId = (await _db.Desks.FindAsync(u.DeskId))?.BranchId;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.FullName),
            new(ClaimTypes.Role, u.Role.ToString())
        };
        if (branchId != null) claims.Add(new Claim("BranchId", branchId.Value.ToString()));
        if (u.DeskId != null) claims.Add(new Claim("DeskId", u.DeskId.Value.ToString()));
        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id),
            new AuthenticationProperties { IsPersistent = persistent || u.Role == Role.Kiosk, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(u.Role == Role.Kiosk ? 30 : 1) });
    }
}
