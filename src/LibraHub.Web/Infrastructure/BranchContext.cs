using LibraHub.Data;
using LibraHub.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Infrastructure;

public class BranchContext
{
    public const string SessionKey = "staff.branch";
    List<Branch>? _all; Branch? _current; int? _holdsBadge, _transfersBadge;

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public BranchContext(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    HttpContext Ctx => _http.HttpContext!;
    public bool CanSwitch =>
        Ctx.User.IsInRole(nameof(Role.Reception)) ||
        Ctx.User.IsInRole(nameof(Role.Manager)) ||
        Ctx.User.IsInRole(nameof(Role.Admin));

    public async Task<List<Branch>> AllAsync() => _all ??= await _db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();

    public async Task<int> GetCurrentBranchIdAsync()
    {
        var home = Ctx.User.HomeBranchId() ?? (await AllAsync()).First().Id;
        if (!CanSwitch) return home;
        var chosen = Ctx.Session.GetInt32(SessionKey);
        return chosen ?? home;
    }

    public async Task<Branch> CurrentAsync()
    {
        if (_current != null) return _current;
        var id = await GetCurrentBranchIdAsync();
        _current = (await AllAsync()).First(b => b.Id == id);
        return _current;
    }

    public async Task<int> HoldsBadgeAsync()
    {
        var id = await GetCurrentBranchIdAsync();
        return _holdsBadge ??= await _db.Reservations.CountAsync(r => r.Status == HoldStatus.Ready && r.PickupBranchId == id);
    }

    public async Task<int> TransfersBadgeAsync()
    {
        var id = await GetCurrentBranchIdAsync();
        return _transfersBadge ??= await _db.Transfers.CountAsync(t =>
            (t.Status == TransferStatus.Requested || t.Status == TransferStatus.Dispatched) && (t.FromBranchId == id || t.ToBranchId == id));
    }
}
