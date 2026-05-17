using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> entity)
    {
        entity.HasKey(profile => profile.Id);
        entity.Property(profile => profile.SellerName)
            .HasMaxLength(200);
        entity.Property(profile => profile.Email)
            .HasMaxLength(320);
        entity.Property(profile => profile.Phone)
            .HasMaxLength(50);
        entity.Property(profile => profile.AccountName)
            .HasMaxLength(200);
        entity.Property(profile => profile.SortCode)
            .HasMaxLength(20);
        entity.Property(profile => profile.AccountNumber)
            .HasMaxLength(20);
        entity.Property(profile => profile.PaymentReferenceNote)
            .HasMaxLength(500);
        entity.Property(profile => profile.CreatedUtc)
            .IsRequired();
        entity.Property(profile => profile.UpdatedUtc)
            .IsRequired();
        entity.HasIndex(profile => profile.UserId)
            .IsUnique();
        entity.HasOne(profile => profile.User)
            .WithOne(user => user.SellerProfile)
            .HasForeignKey<SellerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(profile => profile.CreatedByUser)
            .WithMany(user => user.SellerProfilesCreated)
            .HasForeignKey(profile => profile.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(profile => profile.UpdatedByUser)
            .WithMany(user => user.SellerProfilesUpdated)
            .HasForeignKey(profile => profile.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsOne(profile => profile.Address, address =>
        {
            address.Property(value => value.Line1).HasMaxLength(200);
            address.Property(value => value.Line2).HasMaxLength(200);
            address.Property(value => value.City).HasMaxLength(100);
            address.Property(value => value.StateOrCounty).HasMaxLength(100);
            address.Property(value => value.PostalCode).HasMaxLength(20);
            address.Property(value => value.Country).HasMaxLength(100);
        });
    }
}
