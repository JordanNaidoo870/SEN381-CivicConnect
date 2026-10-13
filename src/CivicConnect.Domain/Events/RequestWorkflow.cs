using CivicConnect.Domain.Entities;

namespace CivicConnect.Domain.Events;

public static class RequestWorkflow
{
    public static bool CanTransition(RequestStatus from, RequestStatus to) => (from, to) switch
    {
        (RequestStatus.Pending, RequestStatus.InProgress) => true,
        (RequestStatus.InProgress, RequestStatus.Resolved) => true,
        (RequestStatus.Resolved, RequestStatus.Closed) => true,
        (RequestStatus.Resolved, RequestStatus.InProgress) => true, // re-open for correction
        _ => false
    };
}

public sealed record RequestStatusChanged(int RequestId, RequestStatus OldStatus,
    RequestStatus NewStatus, string ActorUserId, DateTime ChangedAtUtc);
