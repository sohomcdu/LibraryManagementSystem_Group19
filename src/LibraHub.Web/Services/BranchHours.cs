using LibraHub.Domain;
using LibraHub.Infrastructure;

namespace LibraHub.Services;

/// <summary>Open/closed calculation shared by the header pill (mockup 1 #11), Developers page and /api/v1/status.</summary>
public record HoursInfo(bool Open, string? ClosesAt, string? OpensAt);

public static class BranchHours
{
    public static HoursInfo For(Branch b, DateTime now)
    {
        bool ClosedOn(DayOfWeek d) => b.ClosedDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => string.Equals(x, d.ToString(), StringComparison.OrdinalIgnoreCase));
        var t = TimeOnly.FromDateTime(now);
        if (!ClosedOn(now.DayOfWeek) && t >= b.OpensAt && t < b.ClosesAt)
            return new HoursInfo(true, b.ClosesAt.ToString("HH:mm"), null);
        // next opening time
        if (!ClosedOn(now.DayOfWeek) && t < b.OpensAt) return new HoursInfo(false, null, b.OpensAt.ToString("HH:mm"));
        return new HoursInfo(false, null, b.OpensAt.ToString("HH:mm"));
    }

    /// <summary>"Central open · closes 8 pm" (mockup 1 #11).</summary>
    public static string Pill(Branch b, DateTime now)
    {
        var h = For(b, now);
        var name = b.Name.Replace(" Library", "").Replace(" Branch", "");
        static string T(TimeOnly t) => DateTime.Today.Add(t.ToTimeSpan()).ToString(t.Minute == 0 ? "h tt" : "h:mm tt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();
        return h.Open ? $"{name} open · closes {T(b.ClosesAt)}" : $"{name} closed · opens {T(b.OpensAt)}";
    }
}
