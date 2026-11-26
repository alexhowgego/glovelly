using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class UserAdministrativePermissionSetConfiguration : IEntityTypeConfiguration<UserAdministrativePermissionSet>
{
    public void Configure(EntityTypeBuilder<UserAdministrativePermissionSet> entity)
    {
        entity.HasKey(value => new { value.UserId, value.PermissionSet });
        entity.Property(value => value.PermissionSet).HasConversion<string>().HasMaxLength(32);
        entity.HasOne(value => value.User).WithMany()
            .HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AdministrativePermissionChangeConfiguration : IEntityTypeConfiguration<AdministrativePermissionChange>
{
    public void Configure(EntityTypeBuilder<AdministrativePermissionChange> entity)
    {
        entity.HasKey(value => value.Id);
        entity.Property(value => value.TargetIdentity).IsRequired().HasMaxLength(320);
        entity.Property(value => value.ActorIdentity).IsRequired().HasMaxLength(320);
        entity.Property(value => value.Source).IsRequired().HasMaxLength(32);
        entity.Property(value => value.PreviousSets).IsRequired().HasMaxLength(256);
        entity.Property(value => value.NewSets).IsRequired().HasMaxLength(256);
        entity.Property(value => value.Reason).HasMaxLength(1000);
        entity.HasIndex(value => new { value.TargetUserId, value.ChangedAtUtc });
    }
}
