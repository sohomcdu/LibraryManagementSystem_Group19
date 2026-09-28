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

// ------------------------------------------------------------
// BUILD APPLICATION
// ------------------------------------------------------------

var app = builder.Build();

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

// ------------------------------------------------------------
// DEFAULT MVC ROUTE
// ------------------------------------------------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ------------------------------------------------------------
// START THE WEB SERVER
// ------------------------------------------------------------

app.Run();