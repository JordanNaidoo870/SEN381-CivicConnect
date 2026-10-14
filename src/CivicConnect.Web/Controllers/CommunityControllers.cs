using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

public class CommunityController : Controller
{
    public IActionResult Dashboard()
    {
        return View();
    }

    public IActionResult SubmitReport()
    {
        return View();
    }

    public IActionResult MyReports()
    {
        return View();
    }

    public IActionResult ReportDetails(int id = 1001)
    {
        ViewBag.ReportId = id;
        return View();
    }
}