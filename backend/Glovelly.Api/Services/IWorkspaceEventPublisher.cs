namespace Glovelly.Api.Services;

public interface IWorkspaceEventPublisher
{
    Task PublishAsync(Guid? userId, WorkspaceEvent workspaceEvent, CancellationToken cancellationToken = default);
}
