namespace Glovelly.Api.Services;

public sealed class QuickReceiptCaptureSettings
{
    public const string SectionName = "QuickReceiptCapture";

    public int CandidateCount { get; set; } = 5;
    public int AutoAttachWindowDays { get; set; } = 30;
    public int AmbiguityWindowDays { get; set; } = 2;
}
