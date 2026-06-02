using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigCalendarSyncStateConfiguration : IEntityTypeConfiguration<GigCalendarSyncState>
{
    public void Configure(EntityTypeBuilder<GigCalendarSyncState> entity)
    {
        entity.HasKey(state => state.Id);
        entity.Property(state => state.Provider)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(state => state.ProviderCalendarId)
            .HasMaxLength(300);
        entity.Property(state => state.ProviderEventId)
            .HasMaxLength(300);
        entity.Property(state => state.LastSyncHash)
            .HasMaxLength(100);
        entity.Property(state => state.LastSyncError)
            .HasMaxLength(2000);
        entity.Property(state => state.CreatedAtUtc)
            .IsRequired();
        entity.Property(state => state.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(state => new { state.GigId, state.Provider })
            .IsUnique()
            .HasFilter("\"GigId\" IS NOT NULL");
        entity.HasIndex(state => new { state.UserId, state.Provider });
        entity.HasIndex(state => new { state.ProviderCalendarId, state.ProviderEventId });
        entity.HasOne(state => state.User)
            .WithMany(user => user.GigCalendarSyncStates)
            .HasForeignKey(state => state.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
