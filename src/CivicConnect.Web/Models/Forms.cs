using System.ComponentModel.DataAnnotations;
using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CivicConnect.Web.Models;

public sealed class RegisterForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string DisplayName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}
public sealed class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed class CreateRequestForm
{
    [Required, StringLength(120, MinimumLength = 5)] public string Title { get; set; } = "";
    [Required, StringLength(3000, MinimumLength = 10)] public string Description { get; set; } = "";
    [Required, StringLength(180, MinimumLength = 3)] public string Location { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Select a category.")] public int CategoryId { get; set; }
    public IReadOnlyList<CategoryItem> Categories { get; set; } = [];
}
public sealed class RequestListPage
{
    public PageResult<RequestSummary> Results { get; set; } = new([], 0, 1, 15);
    public IReadOnlyList<CategoryItem> Categories { get; set; } = [];
    public RequestFilters Filters { get; set; } = new();
    public string Heading { get; set; } = "Requests";
}
public sealed class RequestDetailsPage
{
    public RequestDetails Details { get; set; } = default!;
    public IReadOnlyList<StaffOption> Staff { get; set; } = [];
    public bool IsRequester { get; set; }
    public bool CanManage { get; set; }
    public bool IsAdmin { get; set; }
    public string? CurrentUserId { get; set; }
}
public sealed class StatusForm
{
    public int Id { get; set; }
    public int Version { get; set; }
    public RequestStatus NextStatus { get; set; }
    [StringLength(1500)] public string? Comment { get; set; }
}
public sealed class AssignmentForm
{
    public int Id { get; set; }
    public int Version { get; set; }
    [Required] public string StaffId { get; set; } = "";
}
public sealed class CommentForm
{
    public int Id { get; set; }
    [Required, StringLength(1500, MinimumLength = 1)] public string Body { get; set; } = "";
}
public sealed class StaffAccountForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string DisplayName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string TemporaryPassword { get; set; } = "";
    [Required] public string Role { get; set; } = Roles.Staff;
}
