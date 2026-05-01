using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

internal sealed class InvoiceGoogleDriveDeliveryChannel(
    AppDbContext dbContext,
    IGoogleDriveApiClient googleDriveApiClient,
    IGoogleDriveTokenProtector tokenProtector,
    StartupSettings settings) : IInvoiceDeliveryChannel
{
    public InvoiceDeliveryChannel Channel => InvoiceDeliveryChannel.GoogleDrive;

    public async Task<InvoiceDeliveryResult> DeliverAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.UserId.HasValue)
        {
            throw new InvalidOperationException("A signed-in user is required to publish invoices to Google Drive.");
        }

        var invoice = request.Invoice;
        if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
        {
            throw new InvalidOperationException("Invoice PDF is missing.");
        }

        var connection = await dbContext.GoogleDriveConnections
            .SingleOrDefaultAsync(
                value => value.UserId == request.UserId.Value && value.RevokedAtUtc == null,
                cancellationToken)
            ?? throw new InvalidOperationException("Google Drive is not connected.");

        if (connection.RefreshTokenExpiresAtUtc is not null &&
            connection.RefreshTokenExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Google Drive connection has expired. Reconnect Google Drive.");
        }

        var accessToken = tokenProtector.Unprotect(connection.EncryptedAccessToken);
        if (connection.AccessTokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            accessToken = await RefreshAccessTokenAsync(connection, cancellationToken);
        }

        var upload = await googleDriveApiClient.UploadPdfAsync(
            accessToken,
            request.AttachmentFileName,
            invoice.PdfBlob,
            cancellationToken);

        return new InvoiceDeliveryResult(
            string.IsNullOrWhiteSpace(upload.WebViewLink)
                ? $"Google Drive file {upload.Id}"
                : upload.WebViewLink);
    }

    private async Task<string> RefreshAccessTokenAsync(
        Models.GoogleDriveConnection connection,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
            string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
        {
            throw new InvalidOperationException("Google OAuth is not configured.");
        }

        if (string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            throw new InvalidOperationException("Google Drive refresh token is missing. Reconnect Google Drive.");
        }

        var refreshToken = tokenProtector.Unprotect(connection.EncryptedRefreshToken);
        var refreshResult = await googleDriveApiClient.RefreshAccessTokenAsync(
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
            throw new InvalidOperationException("Google Drive connection could not be refreshed. Reconnect Google Drive.");
        }

        var tokenResponse = refreshResult.TokenResponse;
        var now = DateTimeOffset.UtcNow;
        connection.EncryptedAccessToken = tokenProtector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = now.AddSeconds(tokenResponse.ExpiresIn);
        connection.Scope = string.IsNullOrWhiteSpace(tokenResponse.Scope)
            ? connection.Scope
            : tokenResponse.Scope;
        connection.TokenType = string.IsNullOrWhiteSpace(tokenResponse.TokenType)
            ? connection.TokenType
            : tokenResponse.TokenType;
        connection.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return tokenResponse.AccessToken;
    }
}
