using CivicConnect.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CivicConnect.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(CivicConnectDbContext db,
        RoleManager<IdentityRole> roles, UserManager<ApplicationUser> users,
        IConfiguration configuration, ILogger logger)
    {
        // For a clean educational database, EnsureCreated works without generated migrations.
        // Once EF migrations are introduced, apply them instead. Do not mix EnsureCreated
        // with an existing migration history: see docs/DEVELOPMENT.md before upgrading schema.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        foreach (var role in new[] { Roles.Requester, Roles.Staff, Roles.Management, Roles.Administrator })
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }

        foreach (var name in new[] { "Roads", "Water", "Garbage", "Electricity", "Parks", "Other" })
        {
            if (!await db.RequestCategories.AnyAsync(x => x.Name == name))
                db.RequestCategories.Add(new RequestCategory { Name = name });
        }
        await db.SaveChangesAsync();

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var user = await users.FindByEmailAsync(email);
            var newlyCreated = user is null;
            if (user is null)
            {
                user = new ApplicationUser { Email = email, UserName = email, DisplayName = "System administrator", EmailConfirmed = true };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded) throw new InvalidOperationException("Administrator setup failed: " + string.Join(", ", created.Errors.Select(e => e.Description)));
                logger.LogInformation("Configured initial administrator account using environment-provided credentials.");
            }
            // Never turn an existing self-registered requester into an administrator
            // simply because their email matches the configured bootstrap email.
            if (!await users.IsInRoleAsync(user, Roles.Administrator))
            {
                if (!newlyCreated)
                    throw new InvalidOperationException("Bootstrap administrator email belongs to an existing user without admin rights. Choose an unused administrator email.");
                var added = await users.AddToRoleAsync(user, Roles.Administrator);
                if (!added.Succeeded) throw new InvalidOperationException("Unable to assign administrator role.");
            }
        }
        else logger.LogWarning("No initial administrator configured. Set SeedAdmin:Email and SeedAdmin:Password using user-secrets/environment variables.");
    }
}
