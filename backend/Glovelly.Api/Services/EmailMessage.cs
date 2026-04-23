namespace Glovelly.Api.Services;

public sealed record EmailMessage(
    IReadOnlyList<EmailAddress> To,
    string Subject,
    string PlainTextBody,
    EmailAddress? From = null,
    string? HtmlBody = null,
    IReadOnlyList<EmailAddress>? Cc = null,
    IReadOnlyList<EmailAddress>? Bcc = null,
    EmailAddress? ReplyTo = null,
    IReadOnlyList<EmailAttachment>? Attachments = null)
{
    public IReadOnlyList<EmailAddress> Cc { get; init; } = Cc ?? [];
    public IReadOnlyList<EmailAddress> Bcc { get; init; } = Bcc ?? [];
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = Attachments ?? [];
}
