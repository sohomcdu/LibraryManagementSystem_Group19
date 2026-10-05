using System.Globalization;
using System.Security.Claims;
using System.Text;
using LibraHub.Domain;

namespace LibraHub.Infrastructure;

/// <summary>Single place that supplies "now" so tests can control time.</summary>
public static class Clock
{
    public static Func<DateTime> Provider = () => DateTime.Now;
    public static DateTime Now => Provider();
    public static DateTime Today => Now.Date;
}

public static class TextUtil
{
    /// <summary>Lower-case and strip accents – used for accent-insensitive search (mockup 1, rules).</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>ISBN-10 / ISBN-13 helpers (spec F6: check digits validated).</summary>
public static class Isbn
{
    public static string Clean(string? s) =>
        new string((s ?? "").Where(c => char.IsDigit(c) || c == 'X' || c == 'x').ToArray()).ToUpperInvariant();

    public static bool IsValid(string? input)
    {
        var s = Clean(input);
        return s.Length switch { 10 => Valid10(s), 13 => Valid13(s), _ => false };
    }

    static bool Valid10(string s)
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            int v;
            if (s[i] == 'X') { if (i != 9) return false; v = 10; }
            else v = s[i] - '0';
            sum += v * (10 - i);
        }
        return sum % 11 == 0;
    }

    static bool Valid13(string s)
    {
        if (s.Any(c => !char.IsDigit(c))) return false;
        int sum = 0;
        for (int i = 0; i < 13; i++) sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return sum % 10 == 0;
    }

    /// <summary>Convert to canonical ISBN-13 digits (ISBN-10 gets the 978 prefix).</summary>
    public static string ToIsbn13(string input)
    {
        var s = Clean(input);
        if (s.Length == 13) return s;
        if (s.Length != 10) return s;
        var core = "978" + s[..9];
        int sum = 0;
        for (int i = 0; i < 12; i++) sum += (core[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return core + ((10 - sum % 10) % 10);
    }

    /// <summary>Display like 978-1-4493-7332-0 (mockup 3).</summary>
    public static string Display(string isbn13) =>
        isbn13.Length == 13 ? $"{isbn13[..3]}-{isbn13[3]}-{isbn13[4..8]}-{isbn13[8..12]}-{isbn13[12]}" : isbn13;
}

public static class Fmt
{
    public static string Money(int cents) => "$" + (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    public static string Day(DateTime d) => d.ToString("d MMM", CultureInfo.InvariantCulture);
    public static string DayLong(DateTime d) => d.ToString("ddd d MMM", CultureInfo.InvariantCulture);
    public static string Time(DateTime d) => d.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();

    public static string Relative(DateTime d)
    {
        var days = (Clock.Today - d.Date).Days;
        return days switch { 0 => "Today " + Time(d), 1 => "Yesterday " + Time(d), _ => Day(d) + " " + Time(d) };
    }

    public static string StatusText(ItemStatus s) => s == ItemStatus.InTransit ? "In transit" : s.ToString();
    public static string StatusCss(ItemStatus s) => s switch
    {
        ItemStatus.Available => "b-green", ItemStatus.Borrowed => "b-blue", ItemStatus.Damaged => "b-red",
        ItemStatus.Reserved => "b-amber", ItemStatus.InTransit => "b-purple", _ => "b-grey"
    };
    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
}

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal u) => int.TryParse(u.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : 0;
    public static int? DeskId(this ClaimsPrincipal u) => int.TryParse(u.FindFirstValue("DeskId"), out var i) ? i : null;
    public static int? HomeBranchId(this ClaimsPrincipal u) => int.TryParse(u.FindFirstValue("BranchId"), out var i) ? i : null;
    public static Role? RoleOf(this ClaimsPrincipal u) => Enum.TryParse<Role>(u.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;
    public static string DisplayName(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Name) ?? "";
}

public static class Policies
{
    public const string Patron = "Patron";
    public const string Staff = "Staff";      // Reception, Manager, Admin
    public const string Manager = "Manager";  // Manager, Admin
    public const string Admin = "Admin";
    public const string Kiosk = "Kiosk";
}
