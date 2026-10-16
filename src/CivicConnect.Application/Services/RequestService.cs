using CivicConnect.Application.Contracts;
using CivicConnect.Application.Validation;
using CivicConnect.Domain.Entities;
using CivicConnect.Domain.Events;

namespace CivicConnect.Application.Services;

public sealed class RequestService(IRequestRepository repo, IStatusEventPublisher publisher) : IRequestService
{
    public Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct = default) => repo.CategoriesAsync(ct);
    public Task<PageResult<RequestSummary>> ListAsync(RequestFilters filters, string? requesterId = null,
        string? assignedId = null, CancellationToken ct = default) => repo.ListAsync(filters, requesterId, assignedId, ct);
    public Task<RequestDetails?> DetailsAsync(int id, CancellationToken ct = default) => repo.DetailsAsync(id, ct);

    public async Task<OperationResult> CreateAsync(RequestInput input, string requesterId,
        string? correlationId, CancellationToken ct = default)
    {
        var errors = RequestValidator.Validate(input);
        if (errors.Count > 0) return OperationResult.Fail(string.Join(" ", errors));
        return await repo.CreateAsync(input with { Title = input.Title.Trim(), Description = input.Description.Trim(),
            Location = input.Location.Trim() }, requesterId, correlationId, ct);
    }

    public async Task<OperationResult> ChangeStatusAsync(int id, RequestStatus next, int expectedVersion,
        string actorId, string? comment, string? correlationId, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(next)) return OperationResult.Fail("Invalid status.");
        if (comment?.Length > 1500) return OperationResult.Fail("Status note is too long.");
        var existing = await repo.DetailsAsync(id, ct);
        if (existing is null) return OperationResult.Fail("Request not found.");
        if (!RequestWorkflow.CanTransition(existing.Request.Status, next))
            return OperationResult.Fail("This status change is not allowed.");
        var result = await repo.ChangeStatusAsync(id, next, expectedVersion, actorId, comment?.Trim(), correlationId, ct);
        if (result.Success)
        {
            await publisher.PublishAsync(new(id, existing.Request.Status, next, actorId, DateTime.UtcNow), ct);
        }
        return result;
    }

    public Task<OperationResult> AssignAsync(int id, string staffId, int expectedVersion,
        string actorId, string? correlationId, CancellationToken ct = default) =>
        repo.AssignAsync(id, staffId, expectedVersion, actorId, correlationId, ct);

    public Task<OperationResult> AddCommentAsync(int id, string actorId, string body, bool official,
        string? correlationId, CancellationToken ct = default) =>
        RequestValidator.ValidateComment(body) is { } error
            ? Task.FromResult(OperationResult.Fail(error))
            : repo.AddCommentAsync(id, actorId, body.Trim(), official, correlationId, ct);

    public Task<OperationResult> AddAttachmentAsync(int id, string actorId, string name, string contentType,
        string reference, string? correlationId, CancellationToken ct = default) =>
        repo.AddAttachmentAsync(id, actorId, name, contentType, reference, correlationId, ct);
    public Task<RequestAttachment?> AttachmentAsync(int id, CancellationToken ct = default) => repo.AttachmentAsync(id, ct);
    public Task<DashboardMetrics> MetricsAsync(DateTime nowUtc, CancellationToken ct = default) => repo.MetricsAsync(nowUtc, ct);
    public Task<IReadOnlyList<StaffOption>> StaffAsync(CancellationToken ct = default) => repo.StaffAsync(ct);
    public Task RecordReadAsync(int id, string actorId, string? correlationId, CancellationToken ct = default) =>
        repo.RecordReadAsync(id, actorId, correlationId, ct);
}
