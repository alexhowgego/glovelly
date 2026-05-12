using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

public static class McpOAuthEndpoints
{
    public static IEndpointRouteBuilder MapMcpOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/oauth-protected-resource", (
                HttpRequest request,
                IMcpOAuthService oauthService) =>
            {
                var resource = oauthService.GetResource(request);
                return Results.Ok(new
                {
                    resource,
                    authorization_servers = new[] { oauthService.GetIssuer(request) },
                    scopes_supported = new[] { "mcp:read" },
                    bearer_methods_supported = new[] { "header" },
                });
            })
            .AllowAnonymous()
            .WithTags("MCP OAuth");

        app.MapGet("/.well-known/oauth-authorization-server", (
                HttpRequest request,
                IMcpOAuthService oauthService) =>
            {
                var issuer = oauthService.GetIssuer(request);
                return Results.Ok(new
                {
                    issuer,
                    authorization_endpoint = $"{issuer}/oauth/authorize",
                    token_endpoint = $"{issuer}/oauth/token",
                    response_types_supported = new[] { "code" },
                    grant_types_supported = new[] { "authorization_code", "refresh_token" },
                    token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post", "none" },
                    code_challenge_methods_supported = new[] { "S256" },
                    scopes_supported = new[] { "mcp:read" },
                    resource_parameter_supported = true,
                });
            })
            .AllowAnonymous()
            .WithTags("MCP OAuth");

        var oauth = app.MapGroup("/oauth").AllowAnonymous().WithTags("MCP OAuth");

        oauth.MapGet("/authorize", AuthorizeAsync);
        oauth.MapPost("/token", TokenAsync);

        return app;
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext httpContext,
        IMcpOAuthService oauthService,
        AppDbContext db,
        string? response_type,
        string? client_id,
        string? redirect_uri,
        string? scope,
        string? state,
        string? code_challenge,
        string? code_challenge_method,
        string? resource,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAuthorizationRequest(
            httpContext.Request,
            oauthService,
            response_type,
            client_id,
            redirect_uri,
            scope,
            code_challenge,
            code_challenge_method,
            resource,
            out var client,
            out var validatedScope,
            out var validatedResource);
        if (validationError is not null)
        {
            return validationError;
        }

        var cookieAuth = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!cookieAuth.Succeeded || cookieAuth.Principal?.Identity?.IsAuthenticated != true)
        {
            var returnUrl = $"{httpContext.Request.PathBase}{httpContext.Request.Path}{httpContext.Request.QueryString}";
            return Results.Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var userIdClaim = cookieAuth.Principal.FindFirstValue(GlovellyClaimTypes.UserId);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
        if (!userExists)
        {
            return Results.Unauthorized();
        }

        var issuedCode = await oauthService.CreateAuthorizationCodeAsync(
            client!.ClientId,
            userId,
            redirect_uri!,
            validatedScope,
            validatedResource,
            code_challenge!,
            code_challenge_method!,
            cancellationToken);

        var redirect = AddQuery(redirect_uri!, "code", issuedCode.Code);
        if (!string.IsNullOrWhiteSpace(state))
        {
            redirect = AddQuery(redirect, "state", state);
        }

        return Results.Redirect(redirect);
    }

    private static async Task<IResult> TokenAsync(
        HttpContext httpContext,
        IMcpOAuthService oauthService,
        CancellationToken cancellationToken)
    {
        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return OAuthError("invalid_request", "Token requests must use application/x-www-form-urlencoded.");
        }
        var grantType = form["grant_type"].FirstOrDefault();
        var clientId = form["client_id"].FirstOrDefault();
        var clientSecret = form["client_secret"].FirstOrDefault();

        TryReadBasicClientCredentials(httpContext.Request, out var basicClientId, out var basicClientSecret);
        clientId = basicClientId ?? clientId;
        clientSecret = basicClientSecret ?? clientSecret;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return OAuthError("invalid_client", "Client id is required.", StatusCodes.Status401Unauthorized);
        }

        var client = oauthService.FindClient(clientId);
        if (client is null || !ValidateClientSecret(client, clientSecret))
        {
            return OAuthError("invalid_client", "Client authentication failed.", StatusCodes.Status401Unauthorized);
        }

        var resource = form["resource"].FirstOrDefault() ?? oauthService.GetResource(httpContext.Request);
        if (!string.Equals(resource, oauthService.GetResource(httpContext.Request), StringComparison.Ordinal))
        {
            return OAuthError("invalid_target", "Requested resource is not supported.");
        }

        McpOAuthTokenIssueResult? tokens = grantType switch
        {
            "authorization_code" => await oauthService.RedeemAuthorizationCodeAsync(
                form["code"].FirstOrDefault() ?? string.Empty,
                client.ClientId,
                form["redirect_uri"].FirstOrDefault() ?? string.Empty,
                form["code_verifier"].FirstOrDefault() ?? string.Empty,
                resource,
                cancellationToken),
            "refresh_token" => await oauthService.RefreshAccessTokenAsync(
                form["refresh_token"].FirstOrDefault() ?? string.Empty,
                client.ClientId,
                resource,
                cancellationToken),
            _ => null,
        };

        if (tokens is null)
        {
            return OAuthError("invalid_grant", "Grant is invalid, expired, or already used.");
        }

        return Results.Ok(new TokenResponse(
            tokens.AccessToken,
            tokens.TokenType,
            tokens.ExpiresIn,
            tokens.RefreshToken,
            tokens.Scope));
    }

    private static IResult? ValidateAuthorizationRequest(
        HttpRequest request,
        IMcpOAuthService oauthService,
        string? responseType,
        string? clientId,
        string? redirectUri,
        string? scope,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? resource,
        out McpOAuthClientOptions? client,
        out string validatedScope,
        out string validatedResource)
    {
        client = null;
        validatedScope = McpOAuthService.NormalizeScope(scope);
        validatedResource = resource ?? oauthService.GetResource(request);

        if (!string.Equals(responseType, "code", StringComparison.Ordinal))
        {
            return OAuthError("unsupported_response_type", "Only the authorization code response type is supported.");
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return OAuthError("invalid_request", "Client id is required.");
        }

        client = oauthService.FindClient(clientId);
        if (client is null)
        {
            return OAuthError("unauthorized_client", "OAuth client is not registered.");
        }

        if (string.IsNullOrWhiteSpace(redirectUri) ||
            !client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            return OAuthError("invalid_request", "Redirect URI is not registered for this client.");
        }

        var registeredClient = client;
        if (!McpOAuthService.HasScope(validatedScope, "mcp:read") ||
            !validatedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(requestedScope => registeredClient.Scopes.Contains(requestedScope, StringComparer.Ordinal)))
        {
            return OAuthError("invalid_scope", "Requested scope is not supported.");
        }

        if (!string.Equals(validatedResource, oauthService.GetResource(request), StringComparison.Ordinal))
        {
            return OAuthError("invalid_target", "Requested resource is not supported.");
        }

        if (string.IsNullOrWhiteSpace(codeChallenge) ||
            !string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return OAuthError("invalid_request", "PKCE with code_challenge_method S256 is required.");
        }

        return null;
    }

    private static bool ValidateClientSecret(McpOAuthClientOptions client, string? clientSecret)
    {
        return string.IsNullOrWhiteSpace(client.ClientSecret) ||
            string.Equals(client.ClientSecret, clientSecret, StringComparison.Ordinal);
    }

    private static bool TryReadBasicClientCredentials(HttpRequest request, out string? clientId, out string? clientSecret)
    {
        clientId = null;
        clientSecret = null;

        var authorization = request.Headers.Authorization.ToString();
        const string basicPrefix = "Basic ";
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(basicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[basicPrefix.Length..].Trim()));
            var separator = decoded.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                return false;
            }

            clientId = Uri.UnescapeDataString(decoded[..separator]);
            clientSecret = Uri.UnescapeDataString(decoded[(separator + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult OAuthError(string error, string description, int statusCode = StatusCodes.Status400BadRequest)
    {
        return Results.Json(
            new
            {
                error,
                error_description = description,
            },
            statusCode: statusCode);
    }

    private static string AddQuery(string uri, string name, string value)
    {
        var separator = uri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{uri}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("scope")] string Scope);
}
