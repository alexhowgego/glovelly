using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class ReceiptAnalysisConfiguration : IEntityTypeConfiguration<ReceiptAnalysis>
{
    public void Configure(EntityTypeBuilder<ReceiptAnalysis> entity)
    {
        entity.HasKey(analysis => analysis.Id);
        entity.Property(analysis => analysis.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(analysis => analysis.Provider).IsRequired().HasMaxLength(50);
        entity.Property(analysis => analysis.Model).IsRequired().HasMaxLength(200);
        entity.Property(analysis => analysis.PromptVersion).IsRequired().HasMaxLength(50);
        entity.Property(analysis => analysis.Merchant).HasMaxLength(200);
        entity.Property(analysis => analysis.Currency).HasMaxLength(3);
        entity.Property(analysis => analysis.SuggestedCategory).HasMaxLength(50);
        entity.Property(analysis => analysis.FailureCode).HasMaxLength(50);
        entity.Property(analysis => analysis.FailureMessage).HasMaxLength(300);
        entity.Property(analysis => analysis.Warnings).HasColumnType("jsonb");
        entity.HasIndex(analysis => new { analysis.ExpenseAttachmentId, analysis.RequestedAt });
        entity.HasOne(analysis => analysis.ExpenseAttachment)
            .WithMany(attachment => attachment.Analyses)
            .HasForeignKey(analysis => analysis.ExpenseAttachmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
