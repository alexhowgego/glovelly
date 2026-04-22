namespace Glovelly.Api.Services;

internal static class EmailSenderSupport
{
    public static void ValidateMessage(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("Email messages must include at least one recipient.");
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new InvalidOperationException("Email messages must include a subject.");
        }

        if (string.IsNullOrWhiteSpace(message.PlainTextBody) && string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            throw new InvalidOperationException("Email messages must include a plain-text body or an HTML body.");
        }
    }

    public static EmailAddress ResolveResendFromAddress(EmailSettings settings)
    {
        var address = settings.Resend.DefaultFromAddress
            ?? settings.DefaultFromAddress;
        var displayName = settings.Resend.DefaultFromDisplayName
            ?? settings.DefaultFromDisplayName;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Outbound email requires Email:DefaultFromAddress or Email:Resend:DefaultFromAddress to be configured.");
        }

        return new EmailAddress(address.Trim(), string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
    }

    public static EmailAddress? TryResolveFromAddress(EmailSettings settings)
    {
        var address = settings.DefaultFromAddress;
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var displayName = settings.DefaultFromDisplayName;
        return new EmailAddress(address.Trim(), string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
    }

    public static string FormatAddress(EmailAddress address)
    {
        return string.IsNullOrWhiteSpace(address.DisplayName)
            ? address.Address
            : $"{address.DisplayName} <{address.Address}>";
    }
}
