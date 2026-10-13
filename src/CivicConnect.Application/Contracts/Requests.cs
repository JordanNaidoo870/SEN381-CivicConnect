using CivicConnect.Domain.Entities;
using CivicConnect.Domain.Events;

namespace CivicConnect.Application.Contracts;

public sealed record RequestInput(string Title, string Description, string Location, int CategoryId);
public sealed record RequestFilters(string? Search = null, RequestStatus? Status = null,
    int? CategoryId = null, DateTime? FromUtc = null, DateTime? ToUtc = null,
    int Page = 1, int PageSize = 15, string? Sort = null);
public sealed record RequestSummary(int Id, string Title, string Location, string Category,
    RequestStatus Status, string RequesterId, string? AssignedStaffId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, int Version);
public sealed record RequestHistoryView(string OldStatus, string NewStatus, string By, DateTime AtUtc, string? Comment);
public sealed record RequestCommentView(int Id, string By, bool Official, string Body, DateTime AtUtc);
public sealed record RequestAttachmentView(int Id, string FileName, string ContentType, DateTime AtUtc);
public sealed record RequestDetails(RequestSummary Request, string Description,
    IReadOnlyList<RequestHistoryView> History, IReadOnlyList<RequestCommentView> Comments,
    IReadOnlyList<RequestAttachmentView> Attachments);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record CategoryItem(int Id, string Name);
public sealed record StaffOption(string Id, string Name);
public sealed record CategoryCount(string Name, int Count);
public sealed record DashboardMetrics(int Total, int Pending, int InProgress, int Resolved,
    int Closed, int Overdue, IReadOnlyList<CategoryCount> ByCategory);
public sealed record OperationResult(bool Success, string Error = "", int? Id = null)
{
    public static OperationResult Ok(int? id = null) => new(true, "", id);
    public static OperationResult Fail(string error) => new(false, error);
}
public interface IRequestRepository
{
    Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct = default);
    Task<PageResult<RequestSummary>> ListAsync(RequestFilters filters, string? requesterId = null,
        string? assignedId = null, CancellationToken ct = default);
    Task<RequestDetails?> DetailsAsync(int id, CancellationToken ct = default);
    Task<OperationResult> CreateAsync(RequestInput input, string requesterId, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> ChangeStatusAsync(int id, RequestStatus next, int expectedVersion,
        string actorId, string? comment, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AssignAsync(int id, string staffId, int expectedVersion,
        string actorId, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AddCommentAsync(int id, string actorId, string body,
        bool official, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AddAttachmentAsync(int id, string actorId, string name, string contentType,
        string storageReference, string? correlationId, CancellationToken ct = default);
    Task<RequestAttachment?> AttachmentAsync(int id, CancellationToken ct = default);
    Task<DashboardMetrics> MetricsAsync(DateTime nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<StaffOption>> StaffAsync(CancellationToken ct = default);
    Task RecordReadAsync(int id, string actorId, string? correlationId, CancellationToken ct = default);
}
public interface IStatusEventPublisher { Task PublishAsync(RequestStatusChanged evt, CancellationToken ct = default); }
public interface IRequestService
{
    Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct = default);
    Task<PageResult<RequestSummary>> ListAsync(RequestFilters filters, string? requesterId = null, string? assignedId = null, CancellationToken ct = default);
    Task<RequestDetails?> DetailsAsync(int id, CancellationToken ct = default);
    Task<OperationResult> CreateAsync(RequestInput input, string requesterId, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> ChangeStatusAsync(int id, RequestStatus next, int expectedVersion, string actorId,
        string? comment, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AssignAsync(int id, string staffId, int expectedVersion, string actorId, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AddCommentAsync(int id, string actorId, string body, bool official, string? correlationId, CancellationToken ct = default);
    Task<OperationResult> AddAttachmentAsync(int id, string actorId, string name, string contentType,
        string storageReference, string? correlationId, CancellationToken ct = default);
    Task<RequestAttachment?> AttachmentAsync(int id, CancellationToken ct = default);
    Task<DashboardMetrics> MetricsAsync(DateTime nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<StaffOption>> StaffAsync(CancellationToken ct = default);
    Task RecordReadAsync(int id, string actorId, string? correlationId, CancellationToken ct = default);
}
