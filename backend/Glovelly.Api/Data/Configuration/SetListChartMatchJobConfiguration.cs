using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class SetListChartMatchJobConfiguration : IEntityTypeConfiguration<SetListChartMatchJob>
{
    public void Configure(EntityTypeBuilder<SetListChartMatchJob> entity)
    {
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(job => job.InputJson)
            .HasColumnType("jsonb")
            .IsRequired();
        entity.Property(job => job.ResultJson)
            .HasColumnType("jsonb");
        entity.Property(job => job.SafeErrorMessage)
            .HasMaxLength(1000);
        entity.Property(job => job.CorrelationId)
            .HasMaxLength(200);
        entity.Property(job => job.CreatedAtUtc)
            .IsRequired();
        entity.Property(job => job.UpdatedAtUtc)
            .IsRequired();

        entity.HasIndex(job => new { job.UserId, job.GigId, job.CreatedAtUtc });
        entity.HasIndex(job => new { job.Status, job.CreatedAtUtc });
        entity.HasIndex(job => job.CorrelationId);
        entity.HasOne(job => job.User)
            .WithMany()
            .HasForeignKey(job => job.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(job => job.Gig)
            .WithMany()
            .HasForeignKey(job => job.GigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
