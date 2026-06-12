using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(user => user.Id);
        entity.Property(user => user.GoogleSubject)
            .HasMaxLength(255);
        entity.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(320);
        entity.Property(user => user.DisplayName)
            .HasMaxLength(200);
        entity.Property(user => user.MileageRate)
            .HasPrecision(18, 2);
        entity.Property(user => user.PassengerMileageRate)
            .HasPrecision(18, 2);
        entity.Property(user => user.TravelOriginPostcode)
            .HasMaxLength(20);
        entity.Property(user => user.DefaultPaymentWindowDays);
        entity.Property(user => user.InvoiceFilenamePattern)
            .HasMaxLength(200);
        entity.Property(user => user.InvoiceEmailSubjectPattern)
            .HasMaxLength(200);
        entity.Property(user => user.InvoiceEmailBodyTemplate)
            .HasMaxLength(4000);
        entity.Property(user => user.InvoiceReplyToEmail)
            .HasMaxLength(320);
        entity.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
        entity.Property(user => user.CreatedUtc)
            .IsRequired();
        entity.HasIndex(user => user.GoogleSubject)
            .IsUnique();
        entity.HasIndex(user => user.Email)
            .IsUnique();
    }
}
