using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigImportDraft
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid? ProposedClientId { get; set; }
    public string? ProposedClientName { get; set; }
    public string? ProposedContactName { get; set; }
    public string? ProposedContactEmail { get; set; }
    public string? ProposedProjectName { get; set; }
    public string? ProposedTitle { get; set; }
    public DateOnly? ProposedDate { get; set; }
    public TimeOnly? ProposedArrivalTime { get; set; }
    public TimeOnly? ProposedRehearsalStartTime { get; set; }
    public TimeOnly? ProposedRehearsalEndTime { get; set; }
    public TimeOnly? ProposedShowStartTime { get; set; }
    public TimeOnly? ProposedShowEndTime { get; set; }
    public string? ProposedVenueName { get; set; }
    public string? ProposedVenueAddress { get; set; }
    public string? ProposedVenuePostcode { get; set; }
    public decimal? ProposedFee { get; set; }
    public decimal? ProposedPerDiem { get; set; }
    public string? ProposedNotes { get; set; }
    public string? AccommodationNotes { get; set; }
    public string? TravelNotes { get; set; }
    public string? SourceReference { get; set; }
    public GigImportDraftConfidence Confidence { get; set; } = GigImportDraftConfidence.Medium;
    public string WarningsJson { get; set; } = "[]";
    public GigImportDraftStatus Status { get; set; } = GigImportDraftStatus.Pending;

    [JsonIgnore]
    public GigImportBatch? Batch { get; set; }
    [JsonIgnore]
    public Client? ProposedClient { get; set; }
}
