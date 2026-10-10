using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

// Application entry point and service configuration. Uses the minimal
// hosting model (top-level statements) introduced in modern ASP.NET Core -
// there is no separate Startup.cs file.
var builder = WebApplication.CreateBuilder(args);

// Add services

// Registers MVC controllers and Razor views (Controllers + Views folders).
builder.Services.AddControllersWithViews();

// Registers the EF Core database context, backed by SQL Server, using the
// connection string configured in appsettings.json.
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity configuration.
// Registers UserManager<ApplicationUser>, SignInManager<ApplicationUser>, and
// RoleManager<IdentityRole>, all backed by LibraryDbContext, giving the app
// login/roles support (Admin/Reception/Manager/Member) without a separate
// authentication database.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Relaxed password rules for assignment/development convenience.
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<LibraryDbContext>()
    .AddDefaultTokenProviders();

// Points Identity's authentication cookie at our own custom login/access-denied
// pages (AccountController) instead of the default scaffolded Identity UI routes.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Pipeline setup
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication MUST be called before Authorization, so that User.Identity
// is populated before any [Authorize] attribute checks run.
app.UseAuthentication();
app.UseAuthorization();

// Default MVC route: /Controller/Action/OptionalId, falling back to
// HomeController.Index when no controller/action is specified in the URL.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database roles, staff test accounts, and sample catalogue data on
// every startup. Wrapped in its own scope since SeedData.InitializeAsync
// needs scoped services (DbContext, UserManager, RoleManager) that aren't
// available directly from the top-level builder.
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();
