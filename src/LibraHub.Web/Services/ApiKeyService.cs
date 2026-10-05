using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

/// <summary>Feature F2 – keys are shown once and stored only as a SHA-256 hash.</summary>
public class ApiKeyService(AppDbContext db)
{
    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<(ApiKey Key, string Plain)> CreateAsync(string name)
    {
        var plain = "lh_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', 'a').Replace('/', 'b').TrimEnd('=');
        var k = new ApiKey { Name = name.Trim(), KeyHash = Hash(plain), Prefix = plain[..8], Active = true, CreatedAt = Clock.Now };
        db.ApiKeys.Add(k);
        await db.SaveChangesAsync();
        return (k, plain);
    }

    public Task<ApiKey?> FindAsync(string plain) => db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == Hash(plain));

    public async Task RevokeAsync(int id)
    {
        var k = await db.ApiKeys.FindAsync(id);
        if (k != null) { k.Active = false; await db.SaveChangesAsync(); }
    }
}

/// <summary>Sliding-window limiter: 60 requests per minute per key (spec F2, mockup 23 #5).</summary>
public class ApiRateLimiter
{
    public const int Limit = 60;
    readonly ConcurrentDictionary<int, Queue<DateTime>> _hits = new();

    /// <returns>0 if allowed, otherwise the number of seconds to wait.</returns>
    public int TryHit(int keyId, DateTime now)
    {
        var q = _hits.GetOrAdd(keyId, _ => new Queue<DateTime>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() >= TimeSpan.FromMinutes(1)) q.Dequeue();
            if (q.Count >= Limit) return Math.Max(1, (int)Math.Ceiling((q.Peek().AddMinutes(1) - now).TotalSeconds));
            q.Enqueue(now);
            return 0;
        }
    }
}
