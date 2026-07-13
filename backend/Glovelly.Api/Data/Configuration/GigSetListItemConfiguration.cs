using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigSetListItemConfiguration : IEntityTypeConfiguration<GigSetListItem>
{
    public void Configure(EntityTypeBuilder<GigSetListItem> entity)
    {
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.Confidence)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.ForScoreMappingStatus)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.ForScoreMappingConfidence)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.Section)
            .HasMaxLength(200);
        entity.Property(item => item.PadNumber)
            .HasMaxLength(100);
        entity.Property(item => item.Key)
            .HasMaxLength(100);
        entity.Property(item => item.Title)
            .HasMaxLength(500);
        entity.Property(item => item.Notes)
            .HasMaxLength(4000);
        entity.Property(item => item.RawCellsJson)
            .HasColumnType("jsonb");
        entity.Property(item => item.ForScoreMatchJson)
            .HasColumnType("jsonb");
        entity.Property(item => item.ForScoreChartTitle)
            .HasMaxLength(500);
        entity.Property(item => item.ForScoreChartFilePath)
            .HasMaxLength(1000);

        entity.HasIndex(item => item.GigSetListImportId);
        entity.HasIndex(item => new { item.GigSetListImportId, item.SortOrder });
        entity.HasIndex(item => item.ForScoreLibrarySnapshotId);
        entity.HasIndex(item => item.ForScoreChartId);
        entity.HasIndex(item => item.ForScoreMappingStatus);

        entity.HasOne(item => item.ForScoreLibrarySnapshot)
            .WithMany()
            .HasForeignKey(item => item.ForScoreLibrarySnapshotId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(item => item.ForScoreChart)
            .WithMany()
            .HasForeignKey(item => item.ForScoreChartId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
