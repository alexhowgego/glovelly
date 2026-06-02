using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GoogleDriveIntegrationSettingsConfiguration : IEntityTypeConfiguration<GoogleDriveIntegrationSettings>
{
    public void Configure(EntityTypeBuilder<GoogleDriveIntegrationSettings> entity)
    {
        entity.HasKey(settings => settings.Id);
        entity.Property(settings => settings.InvoiceUploadFolderId)
            .HasMaxLength(200);
        entity.Property(settings => settings.CreatedAtUtc)
            .IsRequired();
        entity.Property(settings => settings.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(settings => settings.UserId)
            .IsUnique();
        entity.HasIndex(settings => settings.GoogleConnectionId)
            .IsUnique();
        entity.HasOne(settings => settings.User)
            .WithOne(user => user.GoogleDriveIntegrationSettings)
            .HasForeignKey<GoogleDriveIntegrationSettings>(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(settings => settings.GoogleConnection)
            .WithOne(connection => connection.DriveSettings)
            .HasForeignKey<GoogleDriveIntegrationSettings>(settings => settings.GoogleConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
