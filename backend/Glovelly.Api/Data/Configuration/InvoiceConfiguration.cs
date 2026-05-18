using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasKey(invoice => invoice.Id);
        entity.Property(invoice => invoice.InvoiceNumber)
            .HasMaxLength(50);
        entity.Property(invoice => invoice.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(invoice => invoice.ReissueCount);
        entity.Property(invoice => invoice.DeliveryCount);
        entity.Property(invoice => invoice.LastDeliveryChannel)
            .HasMaxLength(50);
        entity.Property(invoice => invoice.LastDeliveryRecipient)
            .HasMaxLength(320);
        entity.Property(invoice => invoice.Description)
            .HasMaxLength(4000);
        entity.Property(invoice => invoice.PdfStorageKey)
            .HasMaxLength(600);
        entity.Property(invoice => invoice.PdfFileName)
            .HasMaxLength(255);
        entity.Property(invoice => invoice.PdfContentType)
            .HasMaxLength(100);
        entity.HasIndex(invoice => invoice.PdfStorageKey)
            .IsUnique();

        entity.HasOne(invoice => invoice.CreatedByUser)
            .WithMany(user => user.InvoicesCreated)
            .HasForeignKey(invoice => invoice.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(invoice => invoice.UpdatedByUser)
            .WithMany(user => user.InvoicesUpdated)
            .HasForeignKey(invoice => invoice.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(invoice => invoice.Client)
            .WithMany(client => client.Invoices)
            .HasForeignKey(invoice => invoice.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
