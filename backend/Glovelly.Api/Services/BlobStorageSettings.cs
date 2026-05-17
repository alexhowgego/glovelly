namespace Glovelly.Api.Services;

public sealed class BlobStorageSettings
{
    public const string SectionName = "BlobStorage";

    public string? BucketName { get; set; }
}
