using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Gig> Gigs => Set<Gig>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(client => client.Id);
            entity.Property(client => client.Name)
                .HasMaxLength(200);
            entity.Property(client => client.Email)
                .HasMaxLength(320);

            entity.OwnsOne(client => client.BillingAddress, address =>
            {
                address.Property(value => value.Line1).HasMaxLength(200);
                address.Property(value => value.Line2).HasMaxLength(200);
                address.Property(value => value.City).HasMaxLength(100);
                address.Property(value => value.StateOrCounty).HasMaxLength(100);
                address.Property(value => value.PostalCode).HasMaxLength(20);
                address.Property(value => value.Country).HasMaxLength(100);
            });
        });

        modelBuilder.Entity<Gig>(entity =>
        {
            entity.HasKey(gig => gig.Id);
            entity.Property(gig => gig.Title)
                .HasMaxLength(200);
            entity.Property(gig => gig.Venue)
                .HasMaxLength(200);
            entity.Property(gig => gig.Fee)
                .HasPrecision(18, 2);
            entity.Property(gig => gig.Notes)
                .HasMaxLength(4000);

            entity.HasOne(gig => gig.Client)
                .WithMany(client => client.Gigs)
                .HasForeignKey(gig => gig.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.InvoiceNumber)
                .HasMaxLength(50);
            entity.Property(invoice => invoice.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(invoice => invoice.Subtotal)
                .HasPrecision(18, 2);
            entity.Property(invoice => invoice.Notes)
                .HasMaxLength(4000);

            entity.HasOne(invoice => invoice.Client)
                .WithMany(client => client.Invoices)
                .HasForeignKey(invoice => invoice.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Description)
                .HasMaxLength(500);
            entity.Property(line => line.Quantity)
                .HasPrecision(18, 2);
            entity.Property(line => line.UnitPrice)
                .HasPrecision(18, 2);
            entity.Property(line => line.Total)
                .HasPrecision(18, 2);

            entity.HasOne(line => line.Invoice)
                .WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => line.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
