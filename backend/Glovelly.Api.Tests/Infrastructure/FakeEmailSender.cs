using Glovelly.Api.Services;

namespace Glovelly.Api.Tests.Infrastructure;

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> SentEmails { get; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        SentEmails.Add(message);
        return Task.CompletedTask;
    }
}
