using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.HasKey(client => client.Id);
        entity.Property(client => client.Name)
            .HasMaxLength(200);
        entity.Property(client => client.Email)
            .HasMaxLength(320);
        entity.Property(client => client.MileageRate)
            .HasPrecision(18, 2);
        entity.Property(client => client.PassengerMileageRate)
            .HasPrecision(18, 2);
        entity.Property(client => client.InvoiceFilenamePattern)
            .HasMaxLength(200);
        entity.Property(client => client.InvoiceEmailSubjectPattern)
            .HasMaxLength(200);
        entity.HasOne(client => client.CreatedByUser)
            .WithMany(user => user.ClientsCreated)
            .HasForeignKey(client => client.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(client => client.UpdatedByUser)
            .WithMany(user => user.ClientsUpdated)
            .HasForeignKey(client => client.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.OwnsOne(client => client.BillingAddress, address =>
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
