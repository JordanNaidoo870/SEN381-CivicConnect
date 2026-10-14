using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Error() => View();
}
