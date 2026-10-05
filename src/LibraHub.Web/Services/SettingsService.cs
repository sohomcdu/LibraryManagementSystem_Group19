using System.Globalization;
using LibraHub.Data;
using LibraHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Services;

/// <summary>Configurable business rules (spec: "configurable" throughout F1, F3, F5, F7).</summary>
public record LibrarySettings(
    int LoanDays, int MaxLoans, int FineBlockCents, int FinePerDayCents,
    int MaxHolds, int PickupDays, int DueSoonDays, bool ConsoleLogging,
    double DamagedPct, double IdlePct);

public class SettingsService(AppDbContext db)
{
    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["LoanDays"] = "14", ["MaxLoans"] = "5", ["FineBlockCents"] = "1000", ["FinePerDayCents"] = "50",
        ["MaxHolds"] = "5", ["PickupDays"] = "3", ["DueSoonDays"] = "2", ["ConsoleLogging"] = "true",
        ["DamagedPct"] = "1.5", ["IdlePct"] = "15"
    };

    public async Task<LibrarySettings> LoadAsync()
    {
        var stored = await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
        string V(string k) => stored.TryGetValue(k, out var v) ? v : Defaults[k];
        int I(string k) => int.Parse(V(k), CultureInfo.InvariantCulture);
        double D(string k) => double.Parse(V(k), CultureInfo.InvariantCulture);
        return new LibrarySettings(I("LoanDays"), I("MaxLoans"), I("FineBlockCents"), I("FinePerDayCents"),
            I("MaxHolds"), I("PickupDays"), I("DueSoonDays"), bool.Parse(V("ConsoleLogging")), D("DamagedPct"), D("IdlePct"));
    }

    public async Task SetAsync(string key, string value)
    {
        var s = await db.Settings.FindAsync(key);
        if (s == null) db.Settings.Add(new Setting { Key = key, Value = value });
        else s.Value = value;
        await db.SaveChangesAsync();
    }
}
