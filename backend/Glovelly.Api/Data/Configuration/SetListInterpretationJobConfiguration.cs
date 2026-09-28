using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class SetListInterpretationJobConfiguration : IEntityTypeConfiguration<SetListInterpretationJob>
{
    public void Configure(EntityTypeBuilder<SetListInterpretationJob> entity)
    {
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(50);
        entity.Property(job => job.SpreadsheetId).HasMaxLength(300).IsRequired();
        entity.Property(job => job.WorksheetId).HasMaxLength(100);
        entity.Property(job => job.WorksheetName).HasMaxLength(300).IsRequired();
        entity.Property(job => job.SourceGridJson).HasColumnType("jsonb").IsRequired();
        entity.Property(job => job.ResultJson).HasColumnType("jsonb");
        entity.Property(job => job.SafeErrorMessage).HasMaxLength(1000);
        entity.Property(job => job.CorrelationId).HasMaxLength(200);
        entity.HasIndex(job => new { job.UserId, job.GigId, job.CreatedAtUtc });
        entity.HasIndex(job => new { job.Status, job.SourceGridExpiresAtUtc });
        entity.HasOne(job => job.User).WithMany().HasForeignKey(job => job.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(job => job.Gig).WithMany().HasForeignKey(job => job.GigId).OnDelete(DeleteBehavior.Cascade);
    }
}
