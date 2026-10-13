namespace CivicConnect.Domain.Entities;

public enum RequestStatus { Pending, InProgress, Resolved, Closed }

public static class Roles
{
    public const string Requester = "Requester";
    public const string Staff = "Staff";
    public const string Management = "Management";
    public const string Administrator = "Administrator";
    public const string StaffOrAdmin = Staff + "," + Administrator;
    public const string All = Requester + "," + Staff + "," + Management + "," + Administrator;
}

public sealed class RequestCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class ServiceRequest
{
    public int Id { get; set; }
    public string RequesterId { get; set; } = "";
    public string? AssignedStaffId { get; set; }
    public int CategoryId { get; set; }
    public RequestCategory? Category { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public List<RequestStatusHistory> History { get; set; } = [];
    public List<RequestComment> Comments { get; set; } = [];
    public List<RequestAttachment> Attachments { get; set; } = [];
}

public sealed class RequestStatusHistory
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public RequestStatus OldStatus { get; set; }
    public RequestStatus NewStatus { get; set; }
    public string ChangedByUserId { get; set; } = "";
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
}

public sealed class RequestComment
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public string AuthorUserId { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsOfficial { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RequestAttachment
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StorageReference { get; set; } = "";
    public string UploadedByUserId { get; set; } = "";
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "ServiceRequest";
    public string EntityId { get; set; } = "";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? Detail { get; set; }
}

public sealed class RequestUpvote
{
    public int ServiceRequestId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
