using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigImportDraftConfiguration : IEntityTypeConfiguration<GigImportDraft>
{
    public void Configure(EntityTypeBuilder<GigImportDraft> entity)
    {
        entity.HasKey(draft => draft.Id);
        entity.Property(draft => draft.ProposedClientName)
            .HasMaxLength(200);
        entity.Property(draft => draft.ProposedContactName)
            .HasMaxLength(200);
        entity.Property(draft => draft.ProposedContactEmail)
            .HasMaxLength(320);
        entity.Property(draft => draft.ProposedProjectName)
            .HasMaxLength(200);
        entity.Property(draft => draft.ProposedTitle)
            .HasMaxLength(200);
        entity.Property(draft => draft.ProposedVenueName)
            .HasMaxLength(200);
        entity.Property(draft => draft.ProposedVenueAddress)
            .HasMaxLength(1000);
        entity.Property(draft => draft.ProposedVenuePostcode)
            .HasMaxLength(20);
        entity.Property(draft => draft.ProposedFee)
            .HasPrecision(18, 2);
        entity.Property(draft => draft.ProposedPerDiem)
            .HasPrecision(18, 2);
        entity.Property(draft => draft.ProposedNotes)
            .HasMaxLength(4000);
        entity.Property(draft => draft.AccommodationNotes)
            .HasMaxLength(4000);
        entity.Property(draft => draft.TravelNotes)
            .HasMaxLength(4000);
        entity.Property(draft => draft.SourceReference)
            .HasMaxLength(500);
        entity.Property(draft => draft.Confidence)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(draft => draft.WarningsJson)
            .IsRequired()
            .HasColumnType("jsonb");
        entity.Property(draft => draft.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.HasIndex(draft => draft.BatchId);
        entity.HasIndex(draft => draft.ProposedClientId);
        entity.HasIndex(draft => draft.Status);

        entity.HasOne(draft => draft.ProposedClient)
            .WithMany()
            .HasForeignKey(draft => draft.ProposedClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
