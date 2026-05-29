using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class CalendarSyncWorkItemConfiguration : IEntityTypeConfiguration<CalendarSyncWorkItem>
{
    public void Configure(EntityTypeBuilder<CalendarSyncWorkItem> entity)
    {
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Provider)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.Reason)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(item => item.LastError)
            .HasMaxLength(2000);
        entity.Property(item => item.NextAttemptAtUtc)
            .IsRequired();
        entity.Property(item => item.CreatedAtUtc)
            .IsRequired();
        entity.Property(item => item.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(item => new { item.Status, item.NextAttemptAtUtc });
        entity.HasIndex(item => new { item.UserId, item.Provider });
        entity.HasIndex(item => new { item.GigId, item.Provider });
        entity.HasOne(item => item.User)
            .WithMany(user => user.CalendarSyncWorkItems)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
