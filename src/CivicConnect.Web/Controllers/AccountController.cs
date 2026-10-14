using CivicConnect.Domain.Entities;
using CivicConnect.Infrastructure.Data;
using CivicConnect.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;
public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : Controller
{
    [AllowAnonymous, HttpGet] public IActionResult Register() => View(new RegisterForm());
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm model)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser { UserName = email, Email = email,
            DisplayName = model.DisplayName.Trim(), EmailConfirmed = false };
        var created = await users.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }
        var role = await users.AddToRoleAsync(user, Roles.Requester);
        if (!role.Succeeded) throw new InvalidOperationException("Failed assigning requester role.");
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Requests");
    }

    [AllowAnonymous, HttpGet] public IActionResult Login(string? returnUrl = null) =>
        View(new LoginForm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm model)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(email);
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, model.Password, false, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
                if (await users.IsInRoleAsync(user, Roles.Management)) return RedirectToAction("Index", "Management");
                if (await users.IsInRoleAsync(user, Roles.Staff) || await users.IsInRoleAsync(user, Roles.Administrator))
                    return RedirectToAction("Queue", "Staff");
                return RedirectToAction("Index", "Requests");
            }
        }
        ModelState.AddModelError("", "Invalid credentials or account locked. Please try again later.");
        return View(model);
    }
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
    [AllowAnonymous] public IActionResult Denied() => View();
}
