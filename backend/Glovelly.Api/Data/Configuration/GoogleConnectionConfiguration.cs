using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GoogleConnectionConfiguration : IEntityTypeConfiguration<GoogleConnection>
{
    public void Configure(EntityTypeBuilder<GoogleConnection> entity)
    {
        entity.HasKey(connection => connection.Id);
        entity.Property(connection => connection.GoogleSubject)
            .HasMaxLength(200);
        entity.Property(connection => connection.GoogleEmail)
            .HasMaxLength(320);
        entity.Property(connection => connection.EncryptedAccessToken)
            .IsRequired();
        entity.Property(connection => connection.EncryptedRefreshToken)
            .IsRequired();
        entity.Property(connection => connection.GrantedScopes)
            .HasMaxLength(1000);
        entity.Property(connection => connection.TokenType)
            .HasMaxLength(50);
        entity.Property(connection => connection.ConnectedAtUtc)
            .IsRequired();
        entity.Property(connection => connection.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(connection => connection.UserId)
            .IsUnique();
        entity.HasOne(connection => connection.User)
            .WithOne(user => user.GoogleConnection)
            .HasForeignKey<GoogleConnection>(connection => connection.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
