using System.Net;
using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CivicConnect.IntegrationTests;

public class SecurityTests
{
    [Fact] public async Task SEC01_Identity_stores_hash_not_plaintext_password()
    {
        using var factory = new CivicConnectTestFactory();
        var (client, scope) = await factory.StartAsync(); using (client) using (scope)
        {
            var user = await factory.AddUserAsync(Roles.Requester, "sec01@example.test");
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(user.Id);
            Assert.NotNull(stored);
            Assert.NotNull(stored.PasswordHash);
            Assert.DoesNotContain("TestPassword99!", stored.PasswordHash);
            Assert.True(await manager.CheckPasswordAsync(stored, "TestPassword99!"));
        }
    }

    [Fact] public async Task SEC02_State_changing_form_without_antiforgery_token_is_rejected()
    {
        using var factory = new CivicConnectTestFactory();
        var (client, scope) = await factory.StartAsync(); using (client) using (scope)
        {
            await factory.AddUserAsync(Roles.Requester, "sec02@example.test");
            await HttpTestHelpers.LoginAsync(client, "sec02@example.test");
            var response = await client.PostAsync("/Requests/Create", new FormUrlEncodedContent(new Dictionary<string,string>
            {
                ["Title"] = "Broken pipe", ["Description"] = "Water is flooding the street.",
                ["Location"] = "Main Road", ["CategoryId"] = "1"
            }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact] public async Task SEC03_Requester_cannot_perform_staff_only_status_change()
    {
        using var factory = new CivicConnectTestFactory();
        var (client, scope) = await factory.StartAsync(); using (client) using (scope)
        {
            var owner = await factory.AddUserAsync(Roles.Requester, "sec03@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var created = await svc.CreateAsync(new("Broken road surface", "Large pothole near the school.", "School Road", 2), owner.Id, null);
            await HttpTestHelpers.LoginAsync(client, "sec03@example.test");
            var response = await HttpTestHelpers.PostAsync(client, "/Requests/ChangeStatus",
                $"/Requests/Details/{created.Id}", new Dictionary<string, string>
                {
                    ["Id"] = created.Id!.Value.ToString(), ["Version"] = "1", ["NextStatus"] = "InProgress"
                });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Denied", response.Headers.Location!.ToString());
            Assert.Equal(RequestStatus.Pending, (await svc.DetailsAsync(created.Id.Value))!.Request.Status);
        }
    }
}
