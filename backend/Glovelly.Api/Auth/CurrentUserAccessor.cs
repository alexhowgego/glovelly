using System.Security.Claims;
using Glovelly.Api.Models;

namespace Glovelly.Api.Auth;

public interface ICurrentUserAccessor
{
    Guid? TryGetUserId(ClaimsPrincipal principal);
    UserRole? TryGetRole(ClaimsPrincipal principal);
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(GlovellyClaimTypes.UserId);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public UserRole? TryGetRole(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role) ?? principal.FindFirstValue("role");
        return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role) ? role : null;
    }
}
