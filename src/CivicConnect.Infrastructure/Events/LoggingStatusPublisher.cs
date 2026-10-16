using CivicConnect.Application.Contracts;
using CivicConnect.Domain.Events;
using Microsoft.Extensions.Logging;

namespace CivicConnect.Infrastructure.Events;

public sealed class LoggingStatusPublisher(ILogger<LoggingStatusPublisher> logger) : IStatusEventPublisher
{
    public Task PublishAsync(RequestStatusChanged evt, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("RequestStatusChanged RequestId={RequestId} OldStatus={OldStatus} NewStatus={NewStatus} Actor={Actor}",
                evt.RequestId, evt.OldStatus, evt.NewStatus, evt.ActorUserId);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Non-critical status-change event processing failed for request {RequestId}", evt.RequestId);
        }
        return Task.CompletedTask;
    }
}
