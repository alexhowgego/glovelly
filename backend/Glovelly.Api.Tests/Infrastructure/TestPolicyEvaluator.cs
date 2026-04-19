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
        var claims = new[]
        {
            new Claim(GlovellyClaimTypes.UserId, TestAuthHandler.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, TestAuthHandler.UserId.ToString()),
            new Claim(ClaimTypes.Name, "Test Admin"),
            new Claim(ClaimTypes.Email, "test-admin@glovelly.local"),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()),
        };

        var identity = new ClaimsIdentity(claims, TestAuthHandler.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthHandler.SchemeName);

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
}
