using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Glovelly.Api.Tests.Infrastructure;

internal sealed class TestPolicyEvaluator : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var userId = ResolveUserId(context);
        var claims = new[]
        {
            new Claim(GlovellyClaimTypes.UserId, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test Admin"),
            new Claim(ClaimTypes.Email, "test-admin@glovelly.local"),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()),
        };

        var identity = new ClaimsIdentity(claims, TestAuthContext.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthContext.SchemeName);

        context.User = principal;

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(
        AuthorizationPolicy policy,
        AuthenticateResult authenticationResult,
        HttpContext context,
        object? resource)
    {
        return Task.FromResult(PolicyAuthorizationResult.Success());
    }

    private static Guid ResolveUserId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Test-UserId", out var values))
        {
            return TestAuthContext.UserId;
        }

        var value = values.FirstOrDefault();
        return Guid.TryParse(value, out var parsed) ? parsed : TestAuthContext.UserId;
    }
}
