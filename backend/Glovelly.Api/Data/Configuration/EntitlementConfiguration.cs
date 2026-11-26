using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> entity)
    {
        entity.HasKey(value => value.Code);
        entity.Property(value => value.Code).HasMaxLength(32);
        entity.Property(value => value.DisplayName).IsRequired().HasMaxLength(80);
        entity.ToTable(table => table.HasCheckConstraint("CK_Plans_NonnegativeLimit",
            "\"MonthlyFirstIssueLimit\" IS NULL OR \"MonthlyFirstIssueLimit\" >= 0"));
        entity.HasData(
            new Plan { Code = Plan.Free, DisplayName = "Free", MonthlyFirstIssueLimit = 2 },
            new Plan { Code = Plan.Solo, DisplayName = "Solo", PaidIncomeSummary = true },
            new Plan { Code = Plan.Pro, DisplayName = "Pro", PaidIncomeSummary = true });
    }
}

internal sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> entity)
    {
        entity.HasKey(value => value.UserId);
        entity.Property(value => value.PlanCode).IsRequired().HasMaxLength(32);
        entity.Property(value => value.State).HasConversion<string>().HasMaxLength(32);
        entity.Property(value => value.Source).IsRequired().HasMaxLength(32);
        entity.Property(value => value.Reason).HasMaxLength(1000);
        entity.Property(value => value.Version).IsConcurrencyToken();
        entity.HasOne<User>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Plan>().WithMany().HasForeignKey(value => value.PlanCode).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_UserSubscriptions_EndAfterStart",
            "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\""));
    }
}

internal sealed class LicenceChangeConfiguration : IEntityTypeConfiguration<LicenceChange>
{
    public void Configure(EntityTypeBuilder<LicenceChange> entity)
    {
        entity.HasKey(value => value.Id);
        entity.Property(value => value.TargetIdentity).IsRequired().HasMaxLength(320);
        entity.Property(value => value.ActorIdentity).IsRequired().HasMaxLength(320);
        entity.Property(value => value.Source).IsRequired().HasMaxLength(32);
        entity.Property(value => value.NewAssignment).IsRequired();
        entity.Property(value => value.Reason).HasMaxLength(1000);
        entity.HasIndex(value => new { value.TargetUserId, value.ChangedAtUtc });
        // Identity snapshots, without user FKs, survive deletion like permission audit.
    }
}

internal sealed class UsageLedgerEntryConfiguration : IEntityTypeConfiguration<UsageLedgerEntry>
{
    public void Configure(EntityTypeBuilder<UsageLedgerEntry> entity)
    {
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Metric).IsRequired().HasMaxLength(50);
        entity.Property(value => value.Source).IsRequired().HasMaxLength(32);
        entity.HasIndex(value => new { value.Metric, value.SourceInvoiceId }).IsUnique();
        entity.HasIndex(value => new { value.UserId, value.Metric, value.OccurredAtUtc });
        entity.HasOne<User>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.ToTable(table => table.HasCheckConstraint("CK_UsageLedgerEntries_PositiveQuantity", "\"Quantity\" > 0"));
    }
}
