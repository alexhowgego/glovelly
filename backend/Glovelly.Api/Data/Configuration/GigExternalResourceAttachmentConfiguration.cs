using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigExternalResourceAttachmentConfiguration : IEntityTypeConfiguration<GigExternalResourceAttachment>
{
    public void Configure(EntityTypeBuilder<GigExternalResourceAttachment> entity)
    {
        entity.HasKey(attachment => attachment.Id);
        entity.Property(attachment => attachment.FileName)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(attachment => attachment.ContentType)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(attachment => attachment.StorageKey)
            .IsRequired()
            .HasMaxLength(600);
        entity.Property(attachment => attachment.CreatedAt)
            .IsRequired();
        entity.HasIndex(attachment => attachment.GigExternalResourceId);
        entity.HasIndex(attachment => attachment.StorageKey)
            .IsUnique();
    }
}
