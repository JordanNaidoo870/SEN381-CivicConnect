using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CivicConnect.Web.Controllers;
[Authorize(Roles = Roles.StaffOrAdmin)]
public sealed class StaffController(IRequestService service) : Controller
{
    public async Task<IActionResult> Queue(string? search, RequestStatus? status, int? categoryId,
        DateTime? fromUtc, DateTime? toUtc, string? sort, int page = 1)
    {
        var filters = new RequestFilters(search, status, categoryId, fromUtc, toUtc, page, 15, sort);
        return View("~/Views/Requests/List.cshtml", new RequestListPage {
            Heading = "Staff request queue", Filters = filters,
            Results = await service.ListAsync(filters), Categories = await service.CategoriesAsync() });
    }
    public async Task<IActionResult> Assigned(int page = 1)
    {
        var filters = new RequestFilters(Page: page);
        return View("~/Views/Requests/List.cshtml", new RequestListPage {
            Heading = "Assigned to me", Filters = filters,
            Results = await service.ListAsync(filters, assignedId: User.FindFirstValue(ClaimTypes.NameIdentifier)),
            Categories = await service.CategoriesAsync() });
    }
}
