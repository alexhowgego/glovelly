namespace Glovelly.Api.Services;

public sealed class ExpenseAttachmentSettings
{
    public const string SectionName = "ExpenseAttachments";

    public string? BucketName { get; set; }
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/heif"
    ];
}
