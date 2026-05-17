using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> entity)
    {
        entity.HasKey(line => line.Id);
        entity.Property(line => line.CreatedUtc)
            .IsRequired();
        entity.Property(line => line.Type)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(line => line.Description)
            .HasMaxLength(500);
        entity.Property(line => line.Quantity)
            .HasPrecision(18, 2);
        entity.Property(line => line.UnitPrice)
            .HasPrecision(18, 2);
        entity.Property(line => line.CalculationNotes)
            .HasMaxLength(2000);
        entity.Property(line => line.IsSystemGenerated);

        entity.HasOne(line => line.Invoice)
            .WithMany(invoice => invoice.Lines)
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(line => line.Gig)
            .WithMany()
            .HasForeignKey(line => line.GigId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
