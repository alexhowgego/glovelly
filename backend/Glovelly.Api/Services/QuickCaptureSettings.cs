namespace Glovelly.Api.Services;

public sealed class QuickCaptureSettings
{
    public const string SectionName = "QuickCapture";

    public int CandidateCount { get; set; } = 5;
    public int AutoAttachWindowDays { get; set; } = 30;
    public int AmbiguityWindowDays { get; set; } = 2;
}
