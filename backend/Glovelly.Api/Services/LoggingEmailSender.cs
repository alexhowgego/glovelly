using Microsoft.Extensions.Logging;

namespace Glovelly.Api.Services;

public sealed class LoggingEmailSender(
    ILogger<LoggingEmailSender> logger,
    EmailSettings settings) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EmailSenderSupport.ValidateMessage(message);
        var from = EmailSenderSupport.TryResolveFromAddress(message, settings);

        logger.LogInformation(
            "Outbound email queued for logging only. From: {From}; RecipientCount: {RecipientCount}; HasSubject: {HasSubject}; SubjectLength: {SubjectLength}; HasHtmlBody: {HasHtmlBody}; AttachmentCount: {AttachmentCount}",
            from is null ? "(not configured)" : EmailSenderSupport.FormatAddress(from),
            message.To.Count,
            !string.IsNullOrWhiteSpace(message.Subject),
            message.Subject?.Length ?? 0,
            !string.IsNullOrWhiteSpace(message.HtmlBody),
            message.Attachments.Count);

        logger.LogDebug("Plain-text body present: {HasPlainTextBody}; PlainTextLength: {PlainTextLength}",
            !string.IsNullOrWhiteSpace(message.PlainTextBody),
            message.PlainTextBody?.Length ?? 0);

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            logger.LogDebug("HTML body present: true; HtmlLength: {HtmlLength}", message.HtmlBody.Length);
        }

        return Task.CompletedTask;
    }
}
