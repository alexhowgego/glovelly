using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> entity)
    {
        entity.HasKey(accessRequest => accessRequest.Id);
        entity.Property(accessRequest => accessRequest.Email)
            .IsRequired()
            .HasMaxLength(320);
        entity.Property(accessRequest => accessRequest.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(320);
        entity.Property(accessRequest => accessRequest.DisplayName)
            .HasMaxLength(200);
        entity.Property(accessRequest => accessRequest.Subject)
            .HasMaxLength(255);
        entity.Property(accessRequest => accessRequest.RequestedAtUtc)
            .IsRequired();
        entity.Property(accessRequest => accessRequest.RequestIpHash)
            .HasMaxLength(128);
        entity.Property(accessRequest => accessRequest.NotificationSuppressionReason)
            .HasMaxLength(100);
        entity.HasIndex(accessRequest => accessRequest.NormalizedEmail);
        entity.HasIndex(accessRequest => accessRequest.NotificationSentAtUtc);
        entity.HasIndex(accessRequest => accessRequest.RequestedAtUtc);
    }
}
