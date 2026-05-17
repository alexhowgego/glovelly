using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigConfiguration : IEntityTypeConfiguration<Gig>
{
    public void Configure(EntityTypeBuilder<Gig> entity)
    {
        entity.HasKey(gig => gig.Id);
        entity.Property(gig => gig.Title)
            .HasMaxLength(200);
        entity.Property(gig => gig.Venue)
            .HasMaxLength(200);
        entity.Property(gig => gig.Fee)
            .HasPrecision(18, 2);
        entity.Property(gig => gig.TravelMiles)
            .HasPrecision(18, 2);
        entity.Property(gig => gig.PassengerCount);
        entity.Property(gig => gig.Notes)
            .HasMaxLength(4000);
        entity.Property(gig => gig.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.HasOne(gig => gig.CreatedByUser)
            .WithMany(user => user.GigsCreated)
            .HasForeignKey(gig => gig.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(gig => gig.UpdatedByUser)
            .WithMany(user => user.GigsUpdated)
            .HasForeignKey(gig => gig.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(gig => gig.Client)
            .WithMany(client => client.Gigs)
            .HasForeignKey(gig => gig.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(gig => gig.Invoice)
            .WithMany(invoice => invoice.Gigs)
            .HasForeignKey(gig => gig.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(gig => gig.Expenses)
            .WithOne(expense => expense.Gig)
            .HasForeignKey(expense => expense.GigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
