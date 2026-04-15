using System.Security.Claims;
using Glovelly.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Auth;

public sealed class GoogleOidcClaimsTransformation(AppDbContext dbContext) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        if (!identity.HasClaim(claim => claim.Type == GlovellyClaimTypes.UserId))
        {
            return principal;
        }

        var localUserId = principal.FindFirstValue(GlovellyClaimTypes.UserId);
        if (!Guid.TryParse(localUserId, out var userId))
        {
            return principal;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == userId && value.IsActive);

        if (user is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        ReplaceClaim(identity, ClaimTypes.Name, user.DisplayName ?? user.Email);
        ReplaceClaim(identity, ClaimTypes.Email, user.Email);
        ReplaceClaim(identity, "email", user.Email);
        ReplaceClaim(identity, ClaimTypes.Role, user.Role.ToString());
        ReplaceClaim(identity, "role", user.Role.ToString());

        return principal;
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(claimType, value));
    }
}
