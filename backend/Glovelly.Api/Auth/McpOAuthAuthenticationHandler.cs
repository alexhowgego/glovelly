using System.Security.Claims;
using System.Text.Encodings.Web;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Auth;

public sealed class McpOAuthAuthenticationHandler(
    IOptionsMonitor<McpOAuthAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMcpOAuthService oauthService) : AuthenticationHandler<McpOAuthAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments("/mcp"))
        {
            return AuthenticateResult.NoResult();
        }

        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Bearer token is empty.");
        }

        var validation = await oauthService.ValidateAccessTokenAsync(
            token,
            oauthService.GetResource(Request),
            Context.RequestAborted);
        if (validation is null)
        {
            return AuthenticateResult.Fail("Bearer token is invalid or expired.");
        }

        var user = validation.User;
        var claims = new[]
        {
            new Claim(GlovellyClaimTypes.UserId, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("email", user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new Claim("scope", validation.Scope),
            new Claim("client_id", validation.ClientId),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.AccessControlAllowOrigin = "*";
        Response.Headers.AccessControlExposeHeaders = "Mcp-Session-Id, MCP-Protocol-Version, WWW-Authenticate";
        Response.Headers["MCP-Protocol-Version"] = "2025-06-18";
        Response.Headers.WWWAuthenticate =
            $"Bearer realm=\"glovelly-mcp\", resource_metadata=\"{oauthService.GetProtectedResourceMetadataUrl(Request)}\"";
        return Task.CompletedTask;
    }
}
