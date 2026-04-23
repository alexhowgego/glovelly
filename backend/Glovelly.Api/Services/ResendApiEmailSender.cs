using Resend;

namespace Glovelly.Api.Services;

public sealed class ResendApiEmailSender(IResend resend, EmailSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EmailSenderSupport.ValidateMessage(message);

        if (!settings.Resend.IsConfigured)
        {
            throw new InvalidOperationException(
                "Resend email sending requires Email:Resend:ApiKey and Email:Resend:DefaultFromAddress to be configured.");
        }

        var from = EmailSenderSupport.ResolveFromAddress(message, settings);
        var resendMessage = new Resend.EmailMessage
        {
            From = EmailSenderSupport.FormatAddress(from),
            Subject = message.Subject,
            TextBody = message.PlainTextBody,
            HtmlBody = message.HtmlBody,
        };

        resendMessage.To ??= [];
        resendMessage.Cc ??= [];
        resendMessage.Bcc ??= [];
        resendMessage.Attachments ??= [];

        foreach (var recipient in message.To)
        {
            resendMessage.To.Add(EmailSenderSupport.FormatAddress(recipient));
        }

        foreach (var recipient in message.Cc)
        {
            resendMessage.Cc.Add(EmailSenderSupport.FormatAddress(recipient));
        }

        foreach (var recipient in message.Bcc)
        {
            resendMessage.Bcc.Add(EmailSenderSupport.FormatAddress(recipient));
        }

        if (message.ReplyTo is not null)
        {
            resendMessage.ReplyTo = EmailSenderSupport.FormatAddress(message.ReplyTo);
        }

        foreach (var attachment in message.Attachments)
        {
            resendMessage.Attachments.Add(new Resend.EmailAttachment
            {
                Filename = attachment.FileName,
                Content = attachment.Content,
                ContentType = attachment.ContentType,
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        await resend.EmailSendAsync(resendMessage, cancellationToken);
    }
}
