using System.Security.Claims;
using System.Text.Json;
using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Infrastructure;

public static class SessionExtensions
{
    public static T? GetObj<T>(this ISession s, string key)
    {
        var json = s.GetString(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }
    public static void SetObj<T>(this ISession s, string key, T value) => s.SetString(key, JsonSerializer.Serialize(value));
}

public abstract class AppPageModel : PageModel
{
    protected AppDbContext Db => HttpContext.RequestServices.GetRequiredService<AppDbContext>();
    protected T Svc<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    public int CurrentUserId => User.UserId();
    public string CurrentUserName => User.DisplayName();
    public bool IsManagerUp => User.IsInRole(nameof(Role.Manager)) || User.IsInRole(nameof(Role.Admin));
    public bool IsAdmin => User.IsInRole(nameof(Role.Admin));

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public string? Msg { get; set; }
    public string? Err { get; set; }

    protected void Report(OpResult r, bool redirecting = true)
    {
        if (redirecting) { if (r.Ok) Flash = r.Message; else FlashError = r.Message; }
        else { if (r.Ok) Msg = r.Message; else Err = r.Message; }
    }

    protected Task<int> StaffBranchIdAsync() => Svc<BranchContext>().GetCurrentBranchIdAsync();
    protected int? BranchRestriction =>
    Svc<BranchContext>().CanSwitch
        ? null
        : User.HomeBranchId();

}


