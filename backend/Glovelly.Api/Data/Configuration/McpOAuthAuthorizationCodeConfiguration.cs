using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class McpOAuthAuthorizationCodeConfiguration : IEntityTypeConfiguration<McpOAuthAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<McpOAuthAuthorizationCode> entity)
    {
        entity.HasKey(code => code.Id);
        entity.Property(code => code.CodeHash)
            .IsRequired()
            .HasMaxLength(128);
        entity.Property(code => code.ClientId)
            .IsRequired()
            .HasMaxLength(200);
        entity.Property(code => code.RedirectUri)
            .IsRequired()
            .HasMaxLength(1000);
        entity.Property(code => code.Scope)
            .IsRequired()
            .HasMaxLength(500);
        entity.Property(code => code.Resource)
            .IsRequired()
            .HasMaxLength(1000);
        entity.Property(code => code.CodeChallenge)
            .IsRequired()
            .HasMaxLength(200);
        entity.Property(code => code.CodeChallengeMethod)
            .IsRequired()
            .HasMaxLength(20);
        entity.HasIndex(code => code.CodeHash)
            .IsUnique();
        entity.HasIndex(code => code.ExpiresUtc);
        entity.HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
