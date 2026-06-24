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

        entity.HasIndex(item => item.GigSetListImportId);
        entity.HasIndex(item => new { item.GigSetListImportId, item.SortOrder });
    }
}
