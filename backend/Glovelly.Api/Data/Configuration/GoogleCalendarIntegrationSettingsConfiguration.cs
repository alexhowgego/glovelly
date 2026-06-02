using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GoogleCalendarIntegrationSettingsConfiguration : IEntityTypeConfiguration<GoogleCalendarIntegrationSettings>
{
    public void Configure(EntityTypeBuilder<GoogleCalendarIntegrationSettings> entity)
    {
        entity.HasKey(settings => settings.Id);
        entity.Property(settings => settings.GoogleCalendarId)
            .HasMaxLength(300);
        entity.Property(settings => settings.CalendarName)
            .HasMaxLength(200)
            .IsRequired();
        entity.Property(settings => settings.CreatedAtUtc)
            .IsRequired();
        entity.Property(settings => settings.UpdatedAtUtc)
            .IsRequired();
        entity.HasIndex(settings => settings.UserId)
            .IsUnique();
        entity.HasIndex(settings => settings.GoogleConnectionId)
            .IsUnique();
        entity.HasOne(settings => settings.User)
            .WithOne(user => user.GoogleCalendarIntegrationSettings)
            .HasForeignKey<GoogleCalendarIntegrationSettings>(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(settings => settings.GoogleConnection)
            .WithOne(connection => connection.CalendarSettings)
            .HasForeignKey<GoogleCalendarIntegrationSettings>(settings => settings.GoogleConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
