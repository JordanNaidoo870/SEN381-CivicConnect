using System.Security.Claims;
using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using CivicConnect.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;
[Authorize]
public sealed class RequestsController(IRequestService service, ILogger<RequestsController> logger) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool CanManage => User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Administrator);
    private bool IsRequester => User.IsInRole(Roles.Requester);

    [Authorize(Roles = Roles.Requester)]
    public async Task<IActionResult> Index(int page = 1, string? search = null, RequestStatus? status = null)
    {
        var filters = new RequestFilters(search, status, Page: page);
        return View("List", new RequestListPage { Heading = "My service requests",
            Filters = filters, Results = await service.ListAsync(filters, requesterId: UserId),
            Categories = await service.CategoriesAsync() });
    }

    [Authorize(Roles = Roles.Requester), HttpGet]
    public async Task<IActionResult> Create() => View(new CreateRequestForm { Categories = await service.CategoriesAsync() });

    [Authorize(Roles = Roles.Requester), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestForm model)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(new(model.Title, model.Description, model.Location, model.CategoryId),
                UserId, HttpContext.TraceIdentifier);
            if (result.Success)
            {
                TempData["Success"] = $"Request #{result.Id} was submitted successfully. Status: Pending.";
                return RedirectToAction("Details", new { id = result.Id });
            }
            ModelState.AddModelError("", result.Error);
        }
        model.Categories = await service.CategoriesAsync();
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var details = await service.DetailsAsync(id);
        if (details is null) return NotFound();
        if (!CanManage && !(IsRequester && details.Request.RequesterId == UserId)) return Forbid();
        await service.RecordReadAsync(id, UserId, HttpContext.TraceIdentifier);
        return View(new RequestDetailsPage { Details = details, CurrentUserId = UserId,
            Staff = CanManage ? await service.StaffAsync() : [], IsRequester = IsRequester,
            CanManage = CanManage, IsAdmin = User.IsInRole(Roles.Administrator) });
    }

    [Authorize(Roles = Roles.StaffOrAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(StatusForm model)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await service.ChangeStatusAsync(model.Id, model.NextStatus, model.Version,
            UserId, model.Comment, HttpContext.TraceIdentifier);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Status updated and recorded in request history.";
        return RedirectToAction("Details", new { id = model.Id });
    }

    [Authorize(Roles = Roles.StaffOrAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignmentForm model)
    {
        if (!ModelState.IsValid) return BadRequest();
        // Staff may accept their own work; only an administrator may assign someone else.
        if (!User.IsInRole(Roles.Administrator) && model.StaffId != UserId) return Forbid();
        var result = await service.AssignAsync(model.Id, model.StaffId, model.Version,
            UserId, HttpContext.TraceIdentifier);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Staff assignment saved." : result.Error;
        return RedirectToAction("Details", new { id = model.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(CommentForm model)
    {
        if (!ModelState.IsValid) return BadRequest();
        var details = await service.DetailsAsync(model.Id);
        if (details is null) return NotFound();
        if (!CanManage && !(IsRequester && details.Request.RequesterId == UserId)) return Forbid();
        var result = await service.AddCommentAsync(model.Id, UserId, model.Body, CanManage,
            HttpContext.TraceIdentifier);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Comment saved." : result.Error;
        return RedirectToAction("Details", new { id = model.Id });
    }

    [Authorize(Roles = Roles.Requester), HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, IFormFile? file)
    {
        var details = await service.DetailsAsync(id);
        if (details is null) return NotFound();
        if (details.Request.RequesterId != UserId) return Forbid();
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
        { TempData["Error"] = "Choose an image or MP4 file under 10 MB."; return RedirectToAction("Details", new { id }); }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var accepted = extension is ".jpg" or ".jpeg" or ".png" or ".mp4";
        if (!accepted) { TempData["Error"] = "Only JPG, PNG and MP4 files are supported."; return RedirectToAction("Details", new { id }); }
        var prefix = new byte[12];
        await using (var inspect = file.OpenReadStream())
        {
            var count = await inspect.ReadAsync(prefix);
            if (count < 12) { TempData["Error"] = "Invalid file."; return RedirectToAction("Details", new { id }); }
        }
        var jpeg = prefix[0] == 0xFF && prefix[1] == 0xD8 && prefix[2] == 0xFF;
        var png = prefix.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var mp4 = System.Text.Encoding.ASCII.GetString(prefix, 4, 4) == "ftyp";
        if (!((extension is ".jpg" or ".jpeg") && jpeg || extension == ".png" && png || extension == ".mp4" && mp4))
        { TempData["Error"] = "File contents do not match the selected format."; return RedirectToAction("Details", new { id }); }
        var mime = jpeg ? "image/jpeg" : png ? "image/png" : "video/mp4";
        var filename = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
        Directory.CreateDirectory(path); // outside wwwroot; authenticated download only.
        var destination = Path.Combine(path, filename);
        try
        {
            await using (var stream = System.IO.File.Create(destination)) await file.CopyToAsync(stream);
            var result = await service.AddAttachmentAsync(id, UserId, Path.GetFileName(file.FileName)[..Math.Min(Path.GetFileName(file.FileName).Length, 200)],
                mime, filename, HttpContext.TraceIdentifier);
            if (!result.Success) { System.IO.File.Delete(destination); TempData["Error"] = result.Error; }
            else TempData["Success"] = "Attachment uploaded.";
        }
        catch (Exception error)
        {
            if (System.IO.File.Exists(destination)) System.IO.File.Delete(destination);
            logger.LogError(error, "Attachment upload failed for request {RequestId}", id);
            TempData["Error"] = "Upload failed. Please try again.";
        }
        return RedirectToAction("Details", new { id });
    }

    public async Task<IActionResult> Download(int id)
    {
        var attachment = await service.AttachmentAsync(id);
        if (attachment is null) return NotFound();
        var details = await service.DetailsAsync(attachment.ServiceRequestId);
        if (details is null) return NotFound();
        if (!CanManage && !(IsRequester && details.Request.RequesterId == UserId)) return Forbid();
        var storageName = Path.GetFileName(attachment.StorageReference);
        var path = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads", storageName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, attachment.ContentType, enableRangeProcessing: true);
    }
}
