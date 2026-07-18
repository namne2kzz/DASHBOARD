using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Invitation"/> entity.</summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <summary>Configures the Invitations table mapping and relationships.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).IsRequired().HasMaxLength(320);
        builder.Property(i => i.DefaultRole).IsRequired().HasMaxLength(100);
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(i => i.Status).IsRequired().HasDefaultValue(InvitationStatus.Pending);
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.AcceptedAt).IsRequired(false);

        // Lookup by hash on accept — most frequent query path.
        builder.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName("IX_Invitations_TokenHash");

        // Revoke-prior-invites query: email + repo + status.
        builder.HasIndex(i => new { i.Email, i.RepositoryId, i.Status })
            .HasDatabaseName("IX_Invitations_Email_RepositoryId_Status");

        builder.HasOne(i => i.Repository)
            .WithMany()
            .HasForeignKey(i => i.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Role)
            .WithMany()
            .HasForeignKey(i => i.RoleId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired();
    }
}
