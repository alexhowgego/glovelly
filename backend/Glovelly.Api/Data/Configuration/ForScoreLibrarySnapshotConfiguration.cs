using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class ForScoreLibrarySnapshotConfiguration : IEntityTypeConfiguration<ForScoreLibrarySnapshot>
{
    public void Configure(EntityTypeBuilder<ForScoreLibrarySnapshot> entity)
    {
        entity.HasKey(snapshot => snapshot.Id);
        entity.Property(snapshot => snapshot.OriginalFileName)
            .HasMaxLength(500);
        entity.Property(snapshot => snapshot.SourceFormat)
            .HasMaxLength(50);
        entity.Property(snapshot => snapshot.BackupVersion)
            .HasMaxLength(50);
        entity.Property(snapshot => snapshot.WarningsJson)
            .HasColumnType("jsonb");

        entity.HasIndex(snapshot => snapshot.CreatedByUserId);
        entity.HasIndex(snapshot => new { snapshot.CreatedByUserId, snapshot.IsActive });
        entity.HasOne(snapshot => snapshot.CreatedByUser)
            .WithMany(user => user.ForScoreLibrarySnapshots)
            .HasForeignKey(snapshot => snapshot.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(snapshot => snapshot.Charts)
            .WithOne(chart => chart.Snapshot)
            .HasForeignKey(chart => chart.ForScoreLibrarySnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
