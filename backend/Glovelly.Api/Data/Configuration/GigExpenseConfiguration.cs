using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class GigExpenseConfiguration : IEntityTypeConfiguration<GigExpense>
{
    public void Configure(EntityTypeBuilder<GigExpense> entity)
    {
        entity.HasKey(expense => expense.Id);
        entity.Property(expense => expense.Description)
            .HasMaxLength(500);
        entity.Property(expense => expense.Amount)
            .HasPrecision(18, 2);
        entity.Property(expense => expense.ReimbursementStatus)
            .HasConversion<string>()
            .HasMaxLength(50);
        entity.Property(expense => expense.ReimbursementMethod)
            .HasMaxLength(100);
        entity.Property(expense => expense.ReimbursementNote)
            .HasMaxLength(1000);
        entity.HasOne(expense => expense.ReimbursementUpdatedByUser)
            .WithMany()
            .HasForeignKey(expense => expense.ReimbursementUpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(expense => expense.ReimbursementInvoice)
            .WithMany()
            .HasForeignKey(expense => expense.ReimbursementInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(expense => expense.Attachments)
            .WithOne(attachment => attachment.Expense)
            .HasForeignKey(attachment => attachment.GigExpenseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
