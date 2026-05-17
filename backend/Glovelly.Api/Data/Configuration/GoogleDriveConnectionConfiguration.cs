using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GoogleDriveConnectionConfiguration : IEntityTypeConfiguration<GoogleDriveConnection>
{
    public void Configure(EntityTypeBuilder<GoogleDriveConnection> entity)
    {
        entity.HasKey(connection => connection.Id);
        entity.Property(connection => connection.EncryptedAccessToken)
            .IsRequired();
        entity.Property(connection => connection.EncryptedRefreshToken)
            .IsRequired();
        entity.Property(connection => connection.Scope)
            .HasMaxLength(500);
        entity.Property(connection => connection.TokenType)
            .HasMaxLength(50);
        entity.Property(connection => connection.InvoiceUploadFolderId)
            .HasMaxLength(200);
        entity.Property(connection => connection.ConnectedAtUtc)
            .IsRequired();
        entity.Property(connection => connection.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(connection => connection.UserId)
            .IsUnique();
        entity.HasOne(connection => connection.User)
            .WithOne(user => user.GoogleDriveConnection)
            .HasForeignKey<GoogleDriveConnection>(connection => connection.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
