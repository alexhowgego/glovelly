using Glovelly.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Glovelly.Api.Services;

internal sealed class WorkspaceEventPublisher(IHubContext<WorkspaceEventsHub> hubContext) : IWorkspaceEventPublisher
{
    public Task PublishAsync(Guid? userId, WorkspaceEvent workspaceEvent, CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue)
        {
            return Task.CompletedTask;
        }

        return hubContext.Clients
            .Group(WorkspaceEventsHub.BuildUserGroupName(userId.Value))
            .SendAsync("workspaceChanged", workspaceEvent, cancellationToken);
    }
}
