using Glovelly.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Glovelly.Api.Hubs;

[Authorize(Policy = GlovellyPolicies.GlovellyUser)]
public sealed class WorkspaceEventsHub(ICurrentUserAccessor currentUserAccessor) : Hub
{
    public static string BuildUserGroupName(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User is null)
        {
            Context.Abort();
            return;
        }

        var userId = currentUserAccessor.TryGetUserId(Context.User);
        if (!userId.HasValue)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroupName(userId.Value));
        await base.OnConnectedAsync();
    }
}
