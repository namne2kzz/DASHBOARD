using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="UserToken"/>.</summary>
internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    /// <summary>Configures the UserTokens table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("UserTokens");
        builder.HasKey(t => t.Id);

        // ── Access token ──────────────────────────────────────────────────────
        builder.Property(t => t.JwtId).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.JwtId).IsUnique();
        builder.Property(t => t.IsRevoked).HasDefaultValue(false);
        builder.Property(t => t.RevokedAt).IsRequired(false);

        // ── Refresh token ─────────────────────────────────────────────────────
        // SHA-256 hash of 64-byte input → base64 = 44 chars, always.
        builder.Property(t => t.RefreshTokenHash).IsRequired().HasMaxLength(88);
        builder.HasIndex(t => t.RefreshTokenHash).IsUnique();
        builder.Property(t => t.RefreshTokenIsRevoked).HasDefaultValue(false);
        builder.Property(t => t.RefreshTokenRevokedAt).IsRequired(false);

        // ── Relationships ─────────────────────────────────────────────────────
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Compound index for revocation check by access token (UserId + JwtId).
        builder.HasIndex(t => new { t.UserId, t.JwtId });

        // Index for background cleanup of expired tokens.
        builder.HasIndex(t => t.AccessTokenExpiresAt);
        builder.HasIndex(t => t.RefreshTokenExpiresAt);
    }
}
