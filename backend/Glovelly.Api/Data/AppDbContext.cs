using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Gig> Gigs => Set<Gig>();
    public DbSet<GigExpense> GigExpenses => Set<GigExpense>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
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
            entity.Property(user => user.InvoiceFilenamePattern)
                .HasMaxLength(200);
            entity.Property(user => user.Role)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(user => user.CreatedUtc)
                .IsRequired();
            entity.HasIndex(user => user.GoogleSubject)
                .IsUnique();
            entity.HasIndex(user => user.Email)
                .IsUnique();
        });

        modelBuilder.Entity<Client>(entity =>
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
        });

        modelBuilder.Entity<GigExpense>(entity =>
        {
            entity.HasKey(expense => expense.Id);
            entity.Property(expense => expense.Description)
                .HasMaxLength(500);
            entity.Property(expense => expense.Amount)
                .HasPrecision(18, 2);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.InvoiceNumber)
                .HasMaxLength(50);
            entity.Property(invoice => invoice.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(invoice => invoice.ReissueCount);
            entity.Property(invoice => invoice.Description)
                .HasMaxLength(4000);
            entity.Property(invoice => invoice.PdfBlob);

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
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
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
        });

        modelBuilder.Entity<SellerProfile>(entity =>
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
        });
    }
}
