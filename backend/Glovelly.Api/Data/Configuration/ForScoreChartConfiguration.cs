using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class ForScoreChartConfiguration : IEntityTypeConfiguration<ForScoreChart>
{
    public void Configure(EntityTypeBuilder<ForScoreChart> entity)
    {
        entity.HasKey(chart => chart.Id);
        entity.Property(chart => chart.FilePath)
            .HasMaxLength(1000);
        entity.Property(chart => chart.Title)
            .HasMaxLength(500);
        entity.Property(chart => chart.NormalizedTitle)
            .HasMaxLength(500);
        entity.Property(chart => chart.Keywords)
            .HasMaxLength(2000);

        entity.HasIndex(chart => chart.ForScoreLibrarySnapshotId);
        entity.HasIndex(chart => new { chart.ForScoreLibrarySnapshotId, chart.SortOrder });
        entity.HasIndex(chart => new { chart.ForScoreLibrarySnapshotId, chart.NormalizedTitle });
    }
}
