using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Glovelly.Api.Data.Configuration;

internal sealed class McpOAuthAccessTokenConfiguration : IEntityTypeConfiguration<McpOAuthAccessToken>
{
    public void Configure(EntityTypeBuilder<McpOAuthAccessToken> entity)
    {
        entity.HasKey(token => token.Id);
        entity.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(128);
        entity.Property(token => token.ClientId)
            .IsRequired()
            .HasMaxLength(200);
        entity.Property(token => token.Scope)
            .IsRequired()
            .HasMaxLength(500);
        entity.Property(token => token.Resource)
            .IsRequired()
            .HasMaxLength(1000);
        entity.HasIndex(token => token.TokenHash)
            .IsUnique();
        entity.HasIndex(token => token.ExpiresUtc);
        entity.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
