namespace Glovelly.Api.Services;

internal enum EmailUseCase
{
    AccessRequests,
    Invoices,
}

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

    public static EmailAddress ResolveConfiguredFromAddress(EmailSettings settings, EmailUseCase useCase)
    {
        var useCaseSettings = ResolveUseCaseSettings(settings, useCase);
        var address = useCaseSettings.FromAddress;
        var displayName = useCaseSettings.FromDisplayName;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException($"Outbound email requires Email:{useCase}:FromAddress to be configured.");
        }

        return new EmailAddress(address.Trim(), string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
    }

    public static EmailAddress ResolveFromAddress(EmailMessage message)
    {
        if (message.From is null)
        {
            throw new InvalidOperationException("Outbound email requires a sender identity.");
        }

        return message.From;
    }

    public static string FormatAddress(EmailAddress address)
    {
        return string.IsNullOrWhiteSpace(address.DisplayName)
            ? address.Address
            : $"{address.DisplayName} <{address.Address}>";
    }

    private static EmailSenderIdentitySettings ResolveUseCaseSettings(
        EmailSettings settings,
        EmailUseCase useCase)
    {
        return useCase switch
        {
            EmailUseCase.AccessRequests => settings.AccessRequests,
            EmailUseCase.Invoices => settings.Invoices,
            _ => throw new ArgumentOutOfRangeException(nameof(useCase)),
        };
    }
}
