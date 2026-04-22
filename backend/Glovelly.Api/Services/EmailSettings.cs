namespace Glovelly.Api.Services;

public sealed class EmailSettings
{
    public string Mode { get; set; } = EmailModes.Log;
    public string? DefaultFromAddress { get; set; }
    public string? DefaultFromDisplayName { get; set; }
    public ResendEmailSettings Resend { get; set; } = new();
    public SmtpEmailSettings Smtp { get; set; } = new();

    public bool HasDefaultFromAddress => !string.IsNullOrWhiteSpace(DefaultFromAddress);
}

public static class EmailModes
{
    public const string Disabled = "Disabled";
    public const string Log = "Log";
    public const string Resend = "Resend";
    public const string Smtp = "Smtp";
}

public sealed class ResendEmailSettings
{
    public string? ApiKey { get; set; }
    public string? DefaultFromAddress { get; set; }
    public string? DefaultFromDisplayName { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(DefaultFromAddress);
}

public sealed class SmtpEmailSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DefaultFromAddress { get; set; }
    public string? DefaultFromDisplayName { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(DefaultFromAddress);
}
