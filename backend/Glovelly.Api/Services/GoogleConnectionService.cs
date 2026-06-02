using System.Text;
using System.Text.Json;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

internal sealed class GoogleConnectionService(
    AppDbContext dbContext,
    IGoogleOAuthTokenClient tokenClient,
    IGoogleTokenProtector tokenProtector,
    StartupSettings settings) : IGoogleConnectionService
{
    public async Task<GoogleConnection> SaveConnectionAsync(
        Guid userId,
        GoogleOAuthTokenResponse tokenResponse,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var connection = await dbContext.GoogleConnections
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);

        if (connection is null)
        {
            connection = new GoogleConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConnectedAtUtc = now,
            };

            dbContext.GoogleConnections.Add(connection);
        }

        var identity = ParseIdTokenPayload(tokenResponse.IdToken);
        connection.GoogleSubject = string.IsNullOrWhiteSpace(identity.Subject)
            ? connection.GoogleSubject
            : identity.Subject;
        connection.GoogleEmail = string.IsNullOrWhiteSpace(identity.Email)
            ? connection.GoogleEmail
            : identity.Email;
        connection.EncryptedAccessToken = tokenProtector.Protect(tokenResponse.AccessToken);

        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            connection.EncryptedRefreshToken = tokenProtector.Protect(tokenResponse.RefreshToken);
            connection.RefreshTokenExpiresAtUtc = tokenResponse.RefreshTokenExpiresIn is { } refreshSeconds
                ? now.AddSeconds(refreshSeconds)
                : null;
        }

        connection.AccessTokenExpiresAtUtc = now.AddSeconds(tokenResponse.ExpiresIn);
        connection.GrantedScopes = MergeScopes(connection.GrantedScopes, tokenResponse.Scope);
        connection.TokenType = string.IsNullOrWhiteSpace(tokenResponse.TokenType)
            ? "Bearer"
            : tokenResponse.TokenType;
        connection.UpdatedAtUtc = now;
        connection.RevokedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return connection;
    }

    public async Task<GoogleConnection?> GetActiveConnectionAsync(
        Guid userId,
        IEnumerable<string> requiredScopes,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var connection = await dbContext.GoogleConnections
            .FirstOrDefaultAsync(value =>
                value.UserId == userId &&
                value.RevokedAtUtc == null &&
                (value.RefreshTokenExpiresAtUtc == null || value.RefreshTokenExpiresAtUtc > now),
                cancellationToken);

        return connection is not null && HasScopes(connection, requiredScopes)
            ? connection
            : null;
    }

    public async Task<GoogleConnectionAccessToken> GetAccessTokenAsync(
        GoogleConnection connection,
        IEnumerable<string> requiredScopes,
        CancellationToken cancellationToken)
    {
        if (connection.RevokedAtUtc is not null)
        {
            throw new InvalidOperationException("Google connection has been disconnected. Reconnect Google.");
        }

        if (!HasScopes(connection, requiredScopes))
        {
            throw new InvalidOperationException("Google connection is missing required scopes. Reconnect Google.");
        }

        if (connection.RefreshTokenExpiresAtUtc is not null &&
            connection.RefreshTokenExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Google connection has expired. Reconnect Google.");
        }

        var accessToken = tokenProtector.Unprotect(connection.EncryptedAccessToken);
        if (connection.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return new GoogleConnectionAccessToken(accessToken, connection.TokenType, connection.GrantedScopes);
        }

        if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
            string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
        {
            throw new InvalidOperationException("Google OAuth is not configured.");
        }

        if (string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            throw new InvalidOperationException("Google refresh token is missing. Reconnect Google.");
        }

        var refreshToken = tokenProtector.Unprotect(connection.EncryptedRefreshToken);
        var refreshResult = await tokenClient.RefreshAccessTokenAsync(
            refreshToken,
            settings.GoogleClientId,
            settings.GoogleClientSecret,
            cancellationToken);
        if (!refreshResult.IsSuccess ||
            refreshResult.TokenResponse is null ||
            string.IsNullOrWhiteSpace(refreshResult.TokenResponse.AccessToken))
        {
            connection.RevokedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Google connection could not be refreshed. Reconnect Google.");
        }

        var tokenResponse = refreshResult.TokenResponse;
        var now = DateTimeOffset.UtcNow;
        connection.EncryptedAccessToken = tokenProtector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = now.AddSeconds(tokenResponse.ExpiresIn);
        connection.GrantedScopes = string.IsNullOrWhiteSpace(tokenResponse.Scope)
            ? connection.GrantedScopes
            : tokenResponse.Scope;
        connection.TokenType = string.IsNullOrWhiteSpace(tokenResponse.TokenType)
            ? connection.TokenType
            : tokenResponse.TokenType;
        connection.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!HasScopes(connection, requiredScopes))
        {
            throw new InvalidOperationException("Google connection is missing required scopes. Reconnect Google.");
        }

        return new GoogleConnectionAccessToken(
            tokenResponse.AccessToken,
            connection.TokenType,
            connection.GrantedScopes);
    }

    public bool HasScopes(GoogleConnection connection, IEnumerable<string> requiredScopes)
    {
        return GoogleScopes.ContainsAll(connection.GrantedScopes, requiredScopes);
    }

    private static (string? Subject, string? Email) ParseIdTokenPayload(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return (null, null);
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return (null, null);
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var subject = root.TryGetProperty("sub", out var subElement)
                ? subElement.GetString()
                : null;
            var email = root.TryGetProperty("email", out var emailElement)
                ? emailElement.GetString()
                : null;

            return (subject, email);
        }
        catch (JsonException)
        {
            return (null, null);
        }
        catch (FormatException)
        {
            return (null, null);
        }
    }

    private static string MergeScopes(string existingScopes, string newScopes)
    {
        return string.Join(
            ' ',
            existingScopes
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(newScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal));
    }
}
