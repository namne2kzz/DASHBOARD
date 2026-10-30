using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="PasswordSetupToken"/> entity.</summary>
internal sealed class PasswordSetupTokenConfiguration : IEntityTypeConfiguration<PasswordSetupToken>
{
    /// <summary>Configures the PasswordSetupTokens table mapping and relationships.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<PasswordSetupToken> builder)
    {
        builder.ToTable("PasswordSetupTokens");
        builder.HasKey(t => t.Id);

        // 64 hex characters of SHA-256, same shape as Invitation.TokenHash.
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt).IsRequired(false);

        // Lookup by hash when the user opens the link — the only read path.
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("IX_PasswordSetupTokens_TokenHash");

        // Invalidate any earlier unused token when a new one is issued for the same user.
        builder.HasIndex(t => new { t.UserId, t.UsedAt })
            .HasDatabaseName("IX_PasswordSetupTokens_UserId_UsedAt");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
