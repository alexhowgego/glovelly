using System.Net;
using System.Net.Mail;

namespace Glovelly.Api.Services;

public sealed class SmtpEmailSender(EmailSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EmailSenderSupport.ValidateMessage(message);

        if (!settings.Smtp.IsConfigured)
        {
            throw new InvalidOperationException(
                "SMTP email sending requires Email:Smtp:Host and Email:Smtp:DefaultFromAddress to be configured.");
        }

        using var mailMessage = BuildMailMessage(message);
        using var client = BuildClient();

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mailMessage, cancellationToken);
    }

    private MailMessage BuildMailMessage(EmailMessage message)
    {
        var mailMessage = new MailMessage
        {
            From = EmailSenderSupport.ToMailAddress(EmailSenderSupport.ResolveFromAddress(settings)),
            Subject = message.Subject,
            Body = message.PlainTextBody,
            IsBodyHtml = false,
        };

        foreach (var recipient in message.To)
        {
            mailMessage.To.Add(EmailSenderSupport.ToMailAddress(recipient));
        }

        foreach (var recipient in message.Cc)
        {
            mailMessage.CC.Add(EmailSenderSupport.ToMailAddress(recipient));
        }

        foreach (var recipient in message.Bcc)
        {
            mailMessage.Bcc.Add(EmailSenderSupport.ToMailAddress(recipient));
        }

        if (message.ReplyTo is not null)
        {
            mailMessage.ReplyToList.Add(EmailSenderSupport.ToMailAddress(message.ReplyTo));
        }

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mailMessage.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));
        }

        foreach (var attachment in message.Attachments)
        {
            var contentStream = new MemoryStream(attachment.Content, writable: false);
            mailMessage.Attachments.Add(new Attachment(contentStream, attachment.FileName, attachment.ContentType));
        }

        return mailMessage;
    }

    private SmtpClient BuildClient()
    {
        var client = new SmtpClient(settings.Smtp.Host!, settings.Smtp.Port)
        {
            EnableSsl = settings.Smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(settings.Smtp.Username))
        {
            client.Credentials = new NetworkCredential(
                settings.Smtp.Username.Trim(),
                settings.Smtp.Password ?? string.Empty);
        }

        return client;
    }
}
