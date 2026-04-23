namespace Glovelly.Api.Services;

public sealed record InvoiceEmailSenderIdentity(
    string FromDisplayName,
    string? ReplyToEmail,
    string? ReplyToDisplayName);
