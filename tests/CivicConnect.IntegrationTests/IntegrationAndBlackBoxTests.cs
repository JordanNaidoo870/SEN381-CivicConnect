using System.Net;
using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CivicConnect.IntegrationTests;

public class IntegrationAndBlackBoxTests
{
    private static RequestInput Input() => new("Broken water pipe", "Water is flooding the main road.", "Main Street", 1);

    [Fact] public async Task INT01_Create_request_persists_and_writes_audit()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var person = await f.AddUserAsync(Roles.Requester, "int01@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var result = await svc.CreateAsync(Input(), person.Id, "integration-01");
            Assert.True(result.Success);
            var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
            Assert.Single(await db.ServiceRequests.ToListAsync());
            Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "CREATE" && a.EntityId == result.Id!.Value.ToString());
        }
    }

    [Fact] public async Task INT02_Status_change_is_atomic_with_history_and_audit()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var requester = await f.AddUserAsync(Roles.Requester, "int02user@example.test");
            var staff = await f.AddUserAsync(Roles.Staff, "int02staff@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var created = await svc.CreateAsync(Input(), requester.Id, null);
            var updated = await svc.ChangeStatusAsync(created.Id!.Value, RequestStatus.InProgress, 1, staff.Id, "Working", null);
            Assert.True(updated.Success);
            var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
            Assert.Equal(RequestStatus.InProgress, (await db.ServiceRequests.AsNoTracking().SingleAsync()).Status);
            Assert.Single(await db.RequestStatusHistories.AsNoTracking().ToListAsync());
            Assert.Contains(await db.AuditLogs.AsNoTracking().ToListAsync(), a => a.Action == "STATUS_UPDATE");
        }
    }

    [Fact] public async Task INT03_Stale_request_version_is_rejected_without_history()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var requester = await f.AddUserAsync(Roles.Requester, "int03@example.test");
            var staff = await f.AddUserAsync(Roles.Staff, "int03staff@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var created = await svc.CreateAsync(Input(), requester.Id, null);
            var updated = await svc.ChangeStatusAsync(created.Id!.Value, RequestStatus.InProgress, 1, staff.Id, null, null);
            Assert.True(updated.Success);
            var stale = await svc.ChangeStatusAsync(created.Id.Value, RequestStatus.Resolved, 1, staff.Id, null, null);
            Assert.False(stale.Success);
            Assert.Contains("Conflict", stale.Error);
            var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
            Assert.Single(await db.RequestStatusHistories.AsNoTracking().ToListAsync());
        }
    }

    [Fact] public async Task INT04_Assignment_checks_staff_role_and_records_audit()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var requester = await f.AddUserAsync(Roles.Requester, "int04@example.test");
            var staff = await f.AddUserAsync(Roles.Staff, "int04staff@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var created = await svc.CreateAsync(Input(), requester.Id, null);
            var rejected = await svc.AssignAsync(created.Id!.Value, requester.Id, 1, staff.Id, null);
            Assert.False(rejected.Success);
            var accepted = await svc.AssignAsync(created.Id.Value, staff.Id, 1, staff.Id, null);
            Assert.True(accepted.Success);
            var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
            Assert.Equal(staff.Id, (await db.ServiceRequests.SingleAsync()).AssignedStaffId);
        }
    }

    [Fact] public async Task BB01_Unauthenticated_request_creation_redirects_to_login()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var response = await client.GetAsync("/Requests/Create");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }
    }

    [Fact] public async Task BB02_Requester_cannot_open_staff_queue()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            await f.AddUserAsync(Roles.Requester, "bb02@example.test");
            await HttpTestHelpers.LoginAsync(client, "bb02@example.test");
            var response = await client.GetAsync("/Staff/Queue");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Denied", response.Headers.Location!.ToString());
        }
    }

    [Fact] public async Task BB03_Requester_cannot_view_another_requesters_private_request()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            var owner = await f.AddUserAsync(Roles.Requester, "bb03owner@example.test");
            await f.AddUserAsync(Roles.Requester, "bb03other@example.test");
            var svc = scope.ServiceProvider.GetRequiredService<IRequestService>();
            var request = await svc.CreateAsync(Input(), owner.Id, null);
            await HttpTestHelpers.LoginAsync(client, "bb03other@example.test");
            var response = await client.GetAsync($"/Requests/Details/{request.Id}");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Denied", response.Headers.Location!.ToString());
        }
    }

    [Fact] public async Task BB04_Invalid_title_does_not_create_a_request()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            await f.AddUserAsync(Roles.Requester, "bb04@example.test");
            await HttpTestHelpers.LoginAsync(client, "bb04@example.test");
            var response = await HttpTestHelpers.PostAsync(client, "/Requests/Create", "/Requests/Create", new()
            { ["Title"] = "abc", ["Description"] = "Water is flooding the street.", ["Location"] = "Main Road", ["CategoryId"] = "1" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); // validation redisplays form
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>().ServiceRequests.CountAsync());
        }
    }

    [Fact] public async Task BB05_Invalid_category_is_rejected_without_data_changes()
    {
        using var f = new CivicConnectTestFactory();
        var (client, scope) = await f.StartAsync(); using (client) using (scope)
        {
            await f.AddUserAsync(Roles.Requester, "bb05@example.test");
            await HttpTestHelpers.LoginAsync(client, "bb05@example.test");
            var response = await HttpTestHelpers.PostAsync(client, "/Requests/Create", "/Requests/Create", new()
            { ["Title"] = "Broken pipe", ["Description"] = "Water is flooding the street.", ["Location"] = "Main Road", ["CategoryId"] = "9999" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>().ServiceRequests.CountAsync());
        }
    }
}
