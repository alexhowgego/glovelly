namespace Glovelly.Api.Services;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);
