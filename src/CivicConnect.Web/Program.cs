using CivicConnect.Application.Contracts;
using CivicConnect.Application.Services;
using CivicConnect.Infrastructure.Data;
using CivicConnect.Infrastructure.Events;
using CivicConnect.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var connection = builder.Configuration.GetConnectionString("CivicConnect")
    ?? (builder.Environment.IsEnvironment("Testing")
        ? "Host=localhost;Database=unused_testing_placeholder;Username=unused;Password=unused"
        : throw new InvalidOperationException("ConnectionStrings:CivicConnect is required. Set a user-secret or environment variable."));
builder.Services.AddDbContext<CivicConnectDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.SignIn.RequireConfirmedAccount = false; // Email verification service is not in committed scope.
}).AddEntityFrameworkStores<CivicConnectDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(cookie =>
{
    cookie.Cookie.HttpOnly = true;
    cookie.Cookie.SameSite = SameSiteMode.Lax;
    // Local HTTP simulated staging is for localhost demonstrations only.
    // Production cookies remain Secure and must be served through HTTPS/TLS.
    cookie.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    cookie.LoginPath = "/Account/Login";
    cookie.AccessDeniedPath = "/Account/Denied";
    cookie.ExpireTimeSpan = TimeSpan.FromHours(6);
});
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IStatusEventPublisher, LoggingStatusPublisher>();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        app.Configuration, app.Logger);
}
// Minimal database health signal for local staging/operational checks.
app.MapGet("/health", async (CivicConnectDbContext db) =>
    await db.Database.CanConnectAsync()
        ? (IResult)Results.Ok(new { status = "healthy" })
        : Results.StatusCode(503));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program { }
