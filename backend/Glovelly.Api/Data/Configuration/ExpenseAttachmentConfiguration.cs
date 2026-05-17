using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class ExpenseAttachmentConfiguration : IEntityTypeConfiguration<ExpenseAttachment>
{
    public void Configure(EntityTypeBuilder<ExpenseAttachment> entity)
    {
        entity.HasKey(attachment => attachment.Id);
        entity.Property(attachment => attachment.FileName)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(attachment => attachment.ContentType)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(attachment => attachment.StorageKey)
            .IsRequired()
            .HasMaxLength(600);
        entity.Property(attachment => attachment.CreatedAt)
            .IsRequired();
        entity.HasIndex(attachment => attachment.GigExpenseId);
        entity.HasIndex(attachment => attachment.StorageKey)
            .IsUnique();
    }
}
