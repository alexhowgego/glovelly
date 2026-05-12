using System.Security.Cryptography;
using System.Text;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public interface IMcpOAuthService
{
    McpOAuthClientOptions? FindClient(string clientId);
    string GetIssuer(HttpRequest request);
    string GetResource(HttpRequest request);
    string GetProtectedResourceMetadataUrl(HttpRequest request);
    string GetAuthorizationServerMetadataUrl(HttpRequest request);
    Task<McpOAuthAuthorizationCodeResult> CreateAuthorizationCodeAsync(
        string clientId,
        Guid userId,
        string redirectUri,
        string scope,
        string resource,
        string codeChallenge,
        string codeChallengeMethod,
        CancellationToken cancellationToken);
    Task<McpOAuthTokenIssueResult?> RedeemAuthorizationCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        string codeVerifier,
        string resource,
        CancellationToken cancellationToken);
    Task<McpOAuthTokenIssueResult?> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string resource,
        CancellationToken cancellationToken);
    Task<McpOAuthTokenValidationResult?> ValidateAccessTokenAsync(
        string accessToken,
        string resource,
        CancellationToken cancellationToken);
}

public sealed class McpOAuthService(
    AppDbContext db,
    IOptions<McpOAuthOptions> optionsAccessor) : IMcpOAuthService
{
    private const string DefaultScope = "mcp:read";

    private readonly McpOAuthOptions _options = optionsAccessor.Value;

    public McpOAuthClientOptions? FindClient(string clientId)
    {
        return _options.Clients.FirstOrDefault(client =>
            string.Equals(client.ClientId, clientId, StringComparison.Ordinal));
    }

    public string GetIssuer(HttpRequest request)
    {
        return NormalizeBaseUri(_options.Issuer) ?? BuildRequestBaseUri(request);
    }

    public string GetResource(HttpRequest request)
    {
        return NormalizeResourceUri(_options.Resource) ?? $"{BuildRequestBaseUri(request)}/mcp";
    }

    public string GetProtectedResourceMetadataUrl(HttpRequest request)
    {
        return $"{GetResourceOrigin(request)}/.well-known/oauth-protected-resource";
    }

    public string GetAuthorizationServerMetadataUrl(HttpRequest request)
    {
        return $"{GetIssuer(request)}/.well-known/oauth-authorization-server";
    }

    public async Task<McpOAuthAuthorizationCodeResult> CreateAuthorizationCodeAsync(
        string clientId,
        Guid userId,
        string redirectUri,
        string scope,
        string resource,
        string codeChallenge,
        string codeChallengeMethod,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var code = GenerateToken();
        var authorizationCode = new McpOAuthAuthorizationCode
        {
            Id = Guid.NewGuid(),
            CodeHash = HashToken(code),
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            Scope = NormalizeScope(scope),
            Resource = resource,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            CreatedUtc = now,
            ExpiresUtc = now.AddMinutes(Math.Max(1, _options.AuthorizationCodeLifetimeMinutes)),
        };

        db.McpOAuthAuthorizationCodes.Add(authorizationCode);
        await db.SaveChangesAsync(cancellationToken);

        return new McpOAuthAuthorizationCodeResult(code, authorizationCode.ExpiresUtc);
    }

    public async Task<McpOAuthTokenIssueResult?> RedeemAuthorizationCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        string codeVerifier,
        string resource,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var codeHash = HashToken(code);
        var authorizationCode = await db.McpOAuthAuthorizationCodes
            .FirstOrDefaultAsync(value => value.CodeHash == codeHash, cancellationToken);

        if (authorizationCode is null ||
            authorizationCode.ConsumedUtc is not null ||
            authorizationCode.ExpiresUtc <= now ||
            !string.Equals(authorizationCode.ClientId, clientId, StringComparison.Ordinal) ||
            !string.Equals(authorizationCode.RedirectUri, redirectUri, StringComparison.Ordinal) ||
            !string.Equals(authorizationCode.Resource, resource, StringComparison.Ordinal) ||
            !ValidatePkce(authorizationCode.CodeChallenge, authorizationCode.CodeChallengeMethod, codeVerifier))
        {
            return null;
        }

        authorizationCode.ConsumedUtc = now;
        return await IssueTokensAsync(
            authorizationCode.ClientId,
            authorizationCode.UserId,
            authorizationCode.Scope,
            authorizationCode.Resource,
            now,
            cancellationToken);
    }

    public async Task<McpOAuthTokenIssueResult?> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string resource,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var storedToken = await db.McpOAuthRefreshTokens
            .FirstOrDefaultAsync(value => value.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null ||
            storedToken.RevokedUtc is not null ||
            storedToken.ExpiresUtc <= now ||
            !string.Equals(storedToken.ClientId, clientId, StringComparison.Ordinal) ||
            !string.Equals(storedToken.Resource, resource, StringComparison.Ordinal))
        {
            return null;
        }

        storedToken.RevokedUtc = now;
        return await IssueTokensAsync(
            storedToken.ClientId,
            storedToken.UserId,
            storedToken.Scope,
            storedToken.Resource,
            now,
            cancellationToken);
    }

    public async Task<McpOAuthTokenValidationResult?> ValidateAccessTokenAsync(
        string accessToken,
        string resource,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(accessToken);
        var token = await db.McpOAuthAccessTokens
            .Include(value => value.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.TokenHash == tokenHash, cancellationToken);

        if (token?.User is null ||
            token.RevokedUtc is not null ||
            token.ExpiresUtc <= now ||
            !token.User.IsActive ||
            !string.Equals(token.Resource, resource, StringComparison.Ordinal) ||
            !HasScope(token.Scope, DefaultScope))
        {
            return null;
        }

        return new McpOAuthTokenValidationResult(token.User, token.ClientId, token.Scope, token.Resource);
    }

    private async Task<McpOAuthTokenIssueResult> IssueTokensAsync(
        string clientId,
        Guid userId,
        string scope,
        string resource,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();
        var accessTokenLifetime = TimeSpan.FromMinutes(Math.Max(1, _options.AccessTokenLifetimeMinutes));
        var refreshTokenLifetime = TimeSpan.FromDays(Math.Max(1, _options.RefreshTokenLifetimeDays));

        db.McpOAuthAccessTokens.Add(new McpOAuthAccessToken
        {
            Id = Guid.NewGuid(),
            TokenHash = HashToken(accessToken),
            ClientId = clientId,
            UserId = userId,
            Scope = NormalizeScope(scope),
            Resource = resource,
            CreatedUtc = now,
            ExpiresUtc = now.Add(accessTokenLifetime),
        });
        db.McpOAuthRefreshTokens.Add(new McpOAuthRefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = HashToken(refreshToken),
            ClientId = clientId,
            UserId = userId,
            Scope = NormalizeScope(scope),
            Resource = resource,
            CreatedUtc = now,
            ExpiresUtc = now.Add(refreshTokenLifetime),
        });

        await db.SaveChangesAsync(cancellationToken);

        return new McpOAuthTokenIssueResult(
            accessToken,
            refreshToken,
            "Bearer",
            (int)accessTokenLifetime.TotalSeconds,
            NormalizeScope(scope));
    }

    public static bool HasScope(string grantedScopes, string requiredScope)
    {
        return grantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(requiredScope, StringComparer.Ordinal);
    }

    public static string NormalizeScope(string? scope)
    {
        var scopes = (scope ?? DefaultScope)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return scopes.Length == 0 ? DefaultScope : string.Join(' ', scopes);
    }

    public static bool ValidatePkce(string codeChallenge, string codeChallengeMethod, string codeVerifier)
    {
        if (!string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return false;
        }

        var verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(verifierBytes));
        return string.Equals(challenge, codeChallenge, StringComparison.Ordinal);
    }

    private static string GenerateToken()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        // MCP OAuth secrets are only compared when presented back to Glovelly, so store one-way hashes.
        // Google Drive tokens use IDataProtection because Glovelly must recover them to call Google APIs.
        return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string? NormalizeBaseUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }

    private static string? NormalizeResourceUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }

    private static string BuildRequestBaseUri(HttpRequest request)
    {
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private string GetResourceOrigin(HttpRequest request)
    {
        var resource = GetResource(request);
        if (Uri.TryCreate(resource, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return BuildRequestBaseUri(request);
    }
}

public sealed record McpOAuthAuthorizationCodeResult(string Code, DateTimeOffset ExpiresUtc);

public sealed record McpOAuthTokenIssueResult(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    string Scope);

public sealed record McpOAuthTokenValidationResult(User User, string ClientId, string Scope, string Resource);
