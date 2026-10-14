using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;
[Authorize(Roles = Roles.Management + "," + Roles.Administrator)]
public sealed class ManagementController(IRequestService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.MetricsAsync(DateTime.UtcNow));
}
