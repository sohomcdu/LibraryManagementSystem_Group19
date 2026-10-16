using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

QuestPDF.Settings.License =
    QuestPDF.Infrastructure.LicenseType.Community;

// Create the application builder
var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// SERVICES
// ------------------------------------------------------------

// MVC controllers + Razor views
builder.Services.AddControllersWithViews();

// Entity Framework Core + SQL Server
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<LibraryDbContext>()
.AddDefaultTokenProviders();

// Identity cookie configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Notification service
builder.Services.AddScoped<NotificationService>();

// REST API key check + usage logging (see Services/ApiAccessFilter.cs)
builder.Services.AddScoped<ApiAccessFilter>();

// ------------------------------------------------------------
// BUILD APPLICATION
// ------------------------------------------------------------

var app = builder.Build();

// Apply committed EF Core migrations and seed demo data on first run.
// This keeps a fresh checkout runnable without manually creating tables.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(scope.ServiceProvider);
    // Load the sample books, music and toys from docs/sample-data (skips anything already there).
    await SampleDataSeeder.SeedAsync(db, app.Environment.ContentRootPath, app.Logger);
}

// ------------------------------------------------------------
// HTTP REQUEST PIPELINE
// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// Allow CSS, JavaScript and images from wwwroot
app.UseStaticFiles();

// Enable routing
app.UseRouting();

// Enable Identity authentication
app.UseAuthentication();

// Enable role-based authorization
app.UseAuthorization();

// REST API security (X-API-Key for /api/v1) and request logging are handled by
// Services/ApiAccessFilter.cs, which is attached to Controllers/Api/CatalogueApiController.cs.

// Kiosk access is controlled by KioskController authorization. Public catalogue
// and home links remain usable from a kiosk session; staff-only routes stay protected.

// ------------------------------------------------------------
// DEFAULT MVC ROUTE
// ------------------------------------------------------------

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ------------------------------------------------------------
// START THE WEB SERVER
// ------------------------------------------------------------

app.Run();