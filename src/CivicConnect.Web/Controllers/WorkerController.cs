using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

public class WorkerController : Controller
{
    public IActionResult Dashboard()
    {
        return View();
    }

    public IActionResult Reports()
    {
        return View();
    }

    public IActionResult ReportDetails(int id = 1001)
    {
        ViewBag.ReportId = id;
        return View();
    }

    public IActionResult UpdateStatus(int id = 1001)
    {
        ViewBag.ReportId = id;
        return View();
    }
}