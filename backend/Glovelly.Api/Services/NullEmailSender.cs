using Microsoft.Extensions.Logging;

namespace Glovelly.Api.Services;

public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EmailSenderSupport.ValidateMessage(message);

        logger.LogInformation(
            "Email sending is disabled. Discarding message for {RecipientCount} recipient(s).",
            message.To.Count);

        return Task.CompletedTask;
    }
}
