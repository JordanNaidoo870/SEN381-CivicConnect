using System.Net;
using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CivicConnect.IntegrationTests;

public class EndToEndTests
{
    [Fact]
    public async Task E2E01_Requester_logs_in_submits_request_and_sees_pending_details()
    {
        using var factory = new CivicConnectTestFactory();
        var (client, scope) = await factory.StartAsync(); using (client) using (scope)
        {
            await factory.AddUserAsync(Roles.Requester, "e2e01@example.test");
            await HttpTestHelpers.LoginAsync(client, "e2e01@example.test");
            var post = await HttpTestHelpers.PostAsync(client, "/Requests/Create", "/Requests/Create", new()
            {
                ["Title"] = "Flooded Main Street", ["Description"] = "Water pouring down the road outside the clinic.",
                ["Location"] = "Main Street clinic", ["CategoryId"] = "1"
            });
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Contains("/Requests/Details", post.Headers.Location!.ToString());
            var details = await client.GetAsync(post.Headers.Location!);
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            var html = await details.Content.ReadAsStringAsync();
            Assert.Contains("Flooded Main Street", html);
            Assert.Contains("Pending", html);
            var index = await client.GetAsync("/Requests");
            Assert.Contains("Flooded Main Street", await index.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task E2E02_Staff_changes_request_status_and_requester_sees_history()
    {
        using var factory = new CivicConnectTestFactory();
        var (requesterClient, scope) = await factory.StartAsync(); using (requesterClient) using (scope)
        {
            var requester = await factory.AddUserAsync(Roles.Requester, "e2e02requester@example.test");
            await factory.AddUserAsync(Roles.Staff, "e2e02staff@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var created = await svc.CreateAsync(new("Streetlight not working", "Streetlights out on this block.", "City Square", 2), requester.Id, null);
            Assert.True(created.Success);
            using var staffClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            await HttpTestHelpers.LoginAsync(staffClient, "e2e02staff@example.test");
            var queue = await staffClient.GetAsync("/Staff/Queue");
            Assert.Contains("Streetlight not working", await queue.Content.ReadAsStringAsync());
            var changed = await HttpTestHelpers.PostAsync(staffClient, "/Requests/ChangeStatus",
                $"/Requests/Details/{created.Id}", new()
                {
                    ["Id"] = created.Id!.Value.ToString(), ["Version"] = "1",
                    ["NextStatus"] = "InProgress", ["Comment"] = "Technician assigned"
                });
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            await HttpTestHelpers.LoginAsync(requesterClient, "e2e02requester@example.test");
            var details = await requesterClient.GetAsync($"/Requests/Details/{created.Id}");
            var html = await details.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            Assert.Contains("InProgress", html);
            Assert.Contains("Technician assigned", html);
        }
    }
}
