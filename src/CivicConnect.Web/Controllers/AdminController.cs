using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using CivicConnect.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;
[Authorize(Roles = Roles.Administrator)]
public sealed class AdminController(UserManager<ApplicationUser> users) : Controller
{
    [HttpGet] public IActionResult CreateUser() => View(new StaffAccountForm());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(StaffAccountForm model)
    {
        if (model.Role is not (Roles.Staff or Roles.Management)) ModelState.AddModelError(nameof(model.Role), "Invalid role.");
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser { UserName = email, Email = email,
            DisplayName = model.DisplayName.Trim(), EmailConfirmed = true };
        var result = await users.CreateAsync(user, model.TemporaryPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }
        result = await users.AddToRoleAsync(user, model.Role);
        if (!result.Succeeded) throw new InvalidOperationException("Account created but role assignment failed.");
        TempData["Success"] = "Authorised staff/management account created.";
        return RedirectToAction(nameof(CreateUser));
    }
}
