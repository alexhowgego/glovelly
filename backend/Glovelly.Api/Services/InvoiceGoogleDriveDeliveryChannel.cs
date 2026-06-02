using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

internal sealed class InvoiceGoogleDriveDeliveryChannel(
    AppDbContext dbContext,
    IGoogleDriveApiClient googleDriveApiClient,
    IGoogleConnectionService googleConnectionService,
    IInvoicePdfService invoicePdfService) : IInvoiceDeliveryChannel
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
        var invoicePdf = await invoicePdfService.OpenReadAsync(invoice, cancellationToken)
            ?? throw new InvalidOperationException("Invoice PDF is missing.");

        var connection = await googleConnectionService.GetActiveConnectionAsync(
            request.UserId.Value,
            [GoogleScopes.DriveFile],
            cancellationToken)
            ?? throw new InvalidOperationException("Google Drive is not connected.");
        var driveSettings = await dbContext.GoogleDriveIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == request.UserId.Value, cancellationToken);
        var accessToken = await googleConnectionService.GetAccessTokenAsync(
            connection,
            [GoogleScopes.DriveFile],
            cancellationToken);

        await using var invoicePdfContent = invoicePdf.Content;
        using var invoicePdfMemory = new MemoryStream();
        await invoicePdf.Content.CopyToAsync(invoicePdfMemory, cancellationToken);

        var upload = await googleDriveApiClient.UploadPdfAsync(
            accessToken.AccessToken,
            request.AttachmentFileName,
            invoicePdfMemory.ToArray(),
            driveSettings?.InvoiceUploadFolderId,
            cancellationToken);

        var webViewLink = string.IsNullOrWhiteSpace(upload.WebViewLink)
            ? null
            : upload.WebViewLink;

        return new InvoiceDeliveryResult(
            webViewLink ?? $"Google Drive file {upload.Id}",
            upload.Id,
            upload.Name,
            webViewLink);
    }

}
