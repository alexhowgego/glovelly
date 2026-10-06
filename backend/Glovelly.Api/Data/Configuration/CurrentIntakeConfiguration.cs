using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class CurrentIntakeConfiguration : IEntityTypeConfiguration<CurrentIntake>
{
    public void Configure(EntityTypeBuilder<CurrentIntake> entity)
    {
        entity.HasKey(value => value.Id);
        entity.Property(value => value.SourceType).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.AnalysisState).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.Intent).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.Confidence).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.FileName).HasMaxLength(255);
        entity.Property(value => value.ContentType).HasMaxLength(255);
        entity.Property(value => value.StorageKey).HasMaxLength(512);
        entity.Property(value => value.Url).HasMaxLength(2048);
        entity.Property(value => value.Text).HasMaxLength(16_000);
        entity.Property(value => value.FailureCode).HasMaxLength(64);
        entity.Property(value => value.FailureMessage).HasMaxLength(500);
        entity.Property(value => value.EvidenceJson).HasMaxLength(32_768);
        entity.HasIndex(value => value.UserId).IsUnique();
        entity.HasOne(value => value.User).WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IntakeAnalysisAttemptConfiguration : IEntityTypeConfiguration<IntakeAnalysisAttempt>
{
    public void Configure(EntityTypeBuilder<IntakeAnalysisAttempt> entity)
    {
        entity.HasKey(value => value.Id);
        entity.Property(value => value.State).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.Intent).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.Confidence).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.Provider).HasMaxLength(64);
        entity.Property(value => value.Model).HasMaxLength(128);
        entity.Property(value => value.PromptVersion).HasMaxLength(64);
        entity.Property(value => value.FailureCode).HasMaxLength(64);
        entity.Property(value => value.FailureMessage).HasMaxLength(500);
        entity.Property(value => value.EvidenceJson).HasMaxLength(32_768);
        entity.HasOne(value => value.CurrentIntake).WithMany(value => value.AnalysisAttempts).HasForeignKey(value => value.CurrentIntakeId).OnDelete(DeleteBehavior.Cascade);
    }
}
