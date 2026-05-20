using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigImportBatchConfiguration : IEntityTypeConfiguration<GigImportBatch>
{
    public void Configure(EntityTypeBuilder<GigImportBatch> entity)
    {
        entity.HasKey(batch => batch.Id);
        entity.Property(batch => batch.SourceName)
            .IsRequired()
            .HasMaxLength(300);
        entity.Property(batch => batch.SourceFingerprint)
            .HasMaxLength(200);
        entity.Property(batch => batch.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(batch => batch.CreatedAtUtc)
            .IsRequired();
        entity.Property(batch => batch.Notes)
            .HasMaxLength(4000);

        entity.HasIndex(batch => batch.CreatedByUserId);
        entity.HasIndex(batch => batch.SourceFingerprint);
        entity.HasIndex(batch => batch.Status);

        entity.HasOne(batch => batch.CreatedByUser)
            .WithMany(user => user.GigImportBatchesCreated)
            .HasForeignKey(batch => batch.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(batch => batch.Drafts)
            .WithOne(draft => draft.Batch)
            .HasForeignKey(draft => draft.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
