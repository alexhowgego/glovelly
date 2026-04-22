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
        var claims = BuildClaims(context);

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

    private static Claim[] BuildClaims(HttpContext context)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, ResolveHeader(context, "X-Test-Name") ?? TestAuthContext.DefaultName),
            new(ClaimTypes.Email, ResolveHeader(context, "X-Test-Email") ?? TestAuthContext.DefaultEmail),
            new Claim("email", ResolveHeader(context, "X-Test-Email") ?? TestAuthContext.DefaultEmail),
            new Claim("sub", ResolveHeader(context, "X-Test-Subject") ?? TestAuthContext.DefaultSubject),
        };

        var role = ResolveHeader(context, "X-Test-Role") ?? UserRole.Admin.ToString();
        claims.Add(new Claim(ClaimTypes.Role, role));
        claims.Add(new Claim("role", role));

        if (!string.Equals(ResolveHeader(context, "X-Test-Include-UserId"), "false", StringComparison.OrdinalIgnoreCase))
        {
            var userId = ResolveUserId(context);
            claims.Add(new Claim(GlovellyClaimTypes.UserId, userId.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }

        return claims.ToArray();
    }

    private static Guid ResolveUserId(HttpContext context)
    {
        var value = ResolveHeader(context, "X-Test-UserId");
        return Guid.TryParse(value, out var parsed) ? parsed : TestAuthContext.UserId;
    }

    private static string? ResolveHeader(HttpContext context, string name)
    {
        return context.Request.Headers.TryGetValue(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
