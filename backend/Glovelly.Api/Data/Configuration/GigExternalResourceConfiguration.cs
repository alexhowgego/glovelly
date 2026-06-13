using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigExternalResourceConfiguration : IEntityTypeConfiguration<GigExternalResource>
{
    public void Configure(EntityTypeBuilder<GigExternalResource> entity)
    {
        entity.HasKey(resource => resource.Id);
        entity.Property(resource => resource.ResourceType)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(resource => resource.Purpose)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(resource => resource.Title)
            .HasMaxLength(200);
        entity.Property(resource => resource.Url)
            .HasMaxLength(2000);
        entity.Property(resource => resource.Notes)
            .HasMaxLength(2000);

        entity.HasIndex(resource => resource.GigId);
        entity.HasMany(resource => resource.Attachments)
            .WithOne(attachment => attachment.Resource)
            .HasForeignKey(attachment => attachment.GigExternalResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
