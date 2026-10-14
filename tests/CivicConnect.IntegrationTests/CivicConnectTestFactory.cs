using Xunit;
using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicConnect.IntegrationTests;

public sealed class CivicConnectTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public CivicConnectTestFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CivicConnectDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CivicConnectDbContext>>();
            services.AddDbContext<CivicConnectDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task<(HttpClient Client, IServiceScope Scope)> StartAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost") });
        var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Requester, Roles.Staff, Roles.Management, Roles.Administrator })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));
        if (!db.RequestCategories.Any())
        {
            db.RequestCategories.AddRange(new RequestCategory { Name = "Water" }, new RequestCategory { Name = "Roads" });
            await db.SaveChangesAsync();
        }
        return (client, scope);
    }

    public async Task<ApplicationUser> AddUserAsync(string role, string email)
    {
        using var scope = Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = email, EmailConfirmed = true };
        var result = await manager.CreateAsync(user, "TestPassword99!");
        Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(x => x.Description)));
        var added = await manager.AddToRoleAsync(user, role);
        Assert.True(added.Succeeded);
        return user;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
