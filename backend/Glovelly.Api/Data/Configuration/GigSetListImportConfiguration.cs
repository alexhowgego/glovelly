using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigSetListImportConfiguration : IEntityTypeConfiguration<GigSetListImport>
{
    public void Configure(EntityTypeBuilder<GigSetListImport> entity)
    {
        entity.HasKey(import => import.Id);
        entity.Property(import => import.SpreadsheetId)
            .HasMaxLength(200);
        entity.Property(import => import.WorksheetId)
            .HasMaxLength(200);
        entity.Property(import => import.WorksheetName)
            .HasMaxLength(200);
        entity.Property(import => import.SourceUrl)
            .HasMaxLength(2000);

        entity.HasIndex(import => import.GigId);
        entity.HasIndex(import => new { import.GigId, import.IsActive });
        entity.HasOne(import => import.Gig)
            .WithMany(gig => gig.SetListImports)
            .HasForeignKey(import => import.GigId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(import => import.GigExternalResource)
            .WithMany()
            .HasForeignKey(import => import.GigExternalResourceId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(import => import.Items)
            .WithOne(item => item.Import)
            .HasForeignKey(item => item.GigSetListImportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
