using CivicConnect.Application.Contracts;

namespace CivicConnect.Application.Validation;

public static class RequestValidator
{
    public static IReadOnlyList<string> Validate(RequestInput value)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(value.Title) || value.Title.Trim().Length < 5 || value.Title.Trim().Length > 120)
            errors.Add("Title must be between 5 and 120 characters.");
        if (string.IsNullOrWhiteSpace(value.Description) || value.Description.Trim().Length < 10 || value.Description.Trim().Length > 3000)
            errors.Add("Description must be between 10 and 3000 characters.");
        if (string.IsNullOrWhiteSpace(value.Location) || value.Location.Trim().Length < 3 || value.Location.Trim().Length > 180)
            errors.Add("Location must be between 3 and 180 characters.");
        if (value.CategoryId <= 0) errors.Add("Select a valid category.");
        return errors;
    }

    public static string? ValidateComment(string? body) => string.IsNullOrWhiteSpace(body) || body.Trim().Length > 1500
        ? "Comment must be between 1 and 1500 characters." : null;
}
