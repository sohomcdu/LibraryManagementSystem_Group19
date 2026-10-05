using System.Text.Json.Serialization;
using LibraHub.Api;
using LibraHub.Data;
using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- data -------------------------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=librahub.db"));

// ---- MVC / Razor Pages + authorisation conventions (spec section 1.3 permission matrix) ---------------
builder.Services.AddRazorPages(o =>
{
    //o.Conventions.AddPageRoute("/Catalogue/Index", "");                       // "/" and "/Catalogue"
    o.Conventions.AuthorizeFolder("/Me", Policies.Patron);
    o.Conventions.AuthorizeFolder("/Staff", Policies.Staff);
    o.Conventions.AuthorizeFolder("/Reports", Policies.Manager);
    o.Conventions.AuthorizeFolder("/Admin", Policies.Admin);
    o.Conventions.AuthorizeFolder("/Kiosk", Policies.Kiosk);                  // F1: kiosk accounts open only /Kiosk/*
    o.Conventions.AllowAnonymousToPage("/Kiosk/Activate");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Cookie.Name = "librahub.auth";
    o.Events.OnRedirectToLogin = ctx =>
    {
        // kiosk devices sign in on their own activation page
        ctx.Response.Redirect(ctx.Request.Path.StartsWithSegments("/Kiosk") ? "/Kiosk/Activate" : ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Patron, p => p.RequireRole(nameof(Role.Patron)));
    o.AddPolicy(Policies.Staff, p => p.RequireRole(nameof(Role.Reception), nameof(Role.Manager), nameof(Role.Admin)));
    o.AddPolicy(Policies.Manager, p => p.RequireRole(nameof(Role.Manager), nameof(Role.Admin)));
    o.AddPolicy(Policies.Admin, p => p.RequireRole(nameof(Role.Admin)));
    o.AddPolicy(Policies.Kiosk, p => p.RequireRole(nameof(Role.Kiosk)));
});

// server-side session: kiosk/desk baskets are never placed in URLs (spec F1)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => { o.IdleTimeout = TimeSpan.FromMinutes(30); o.Cookie.Name = "librahub.session"; o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; });
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// ---- application services ---------------------------------------------------------------------------
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BranchContext>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FineService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ItemLifecycleService>();
builder.Services.AddScoped<LendingService>();
builder.Services.AddScoped<CatalogueService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<IMetadataProvider, LocalJsonMetadataProvider>();
builder.Services.AddSingleton<ApiRateLimiter>();
builder.Services.AddHostedService<DueDateScanner>();

var app = builder.Build();

// ---- create + seed the SQLite database on first run --------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.RunAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>());
    await scope.ServiceProvider.GetRequiredService<ItemLifecycleService>().ExpireHoldsAsync();
    await scope.ServiceProvider.GetRequiredService<NotificationService>().RunScanAsync();
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();

// Kiosk device accounts are restricted to the kiosk workflow.
// Public visitors can still reach /Kiosk/Activate to authenticate a device,
// but an authenticated Kiosk role cannot browse staff, patron or public pages.
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true &&
        ctx.User.IsInRole(nameof(Role.Kiosk)) &&
        !ctx.Request.Path.StartsWithSegments("/Kiosk") &&
        !ctx.Request.Path.StartsWithSegments("/Account/Logout"))
    {
        ctx.Response.Redirect("/Kiosk");
        return;
    }

    await next();
});

app.UseAuthorization();
app.UseMiddleware<ApiKeyMiddleware>();   // F2 – only acts on /api/v1/*

app.MapRazorPages();
app.MapLibraHubApi();
app.Run();

public partial class Program;
