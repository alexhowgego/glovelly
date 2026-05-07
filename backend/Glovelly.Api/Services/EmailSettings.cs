namespace Glovelly.Api.Services;

public sealed class EmailSettings
{
    public string Mode { get; set; } = EmailModes.Log;
    public long MaxTotalAttachmentBytes { get; set; } = 25 * 1024 * 1024;
    public EmailSenderIdentitySettings AccessRequests { get; set; } = new();
    public EmailSenderIdentitySettings Invoices { get; set; } = new();
    public ResendEmailSettings Resend { get; set; } = new();
}

public sealed class EmailSenderIdentitySettings
{
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; }
}

public static class EmailModes
{
    public const string Disabled = "Disabled";
    public const string Log = "Log";
    public const string Resend = "Resend";
}

public sealed class ResendEmailSettings
{
    public string? ApiKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
