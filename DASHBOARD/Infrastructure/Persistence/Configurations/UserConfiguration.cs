using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="User"/>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Configures the Users table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        // Tenant — every user belongs to exactly one organization.
        builder.Property(u => u.OrgId).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(u => u.OrgId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);

        // PasswordHash stores the PBKDF2 output (Base64, ~88 chars for SHA-512/310000 iterations).
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);

        // PasswordSalt stores the cryptographically random salt (Base64, 32 bytes → 44 chars).
        builder.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(100);

        builder.Property(u => u.AvatarClass).HasMaxLength(100).HasDefaultValue("bg-slate-600");
        builder.Property(u => u.IsGlobalAdmin).HasDefaultValue(false);
        builder.Property(u => u.AuthProvider).IsRequired().HasDefaultValue(Domain.Enums.AuthProvider.System);
        builder.Property(u => u.GoogleSubjectId).HasMaxLength(255).IsRequired(false);
        builder.HasIndex(u => u.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL")
            .HasDatabaseName("IX_Users_GoogleSubjectId");

        builder.Property(u => u.ManagerId).IsRequired(false);
        builder.HasOne(u => u.Manager)
            .WithMany(u => u.Subordinates)
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Email is unique per organization (multi-tenant): the same email may exist in different orgs.
        builder.HasIndex(u => new { u.OrgId, u.Email }).IsUnique();

        builder.Property(u => u.IsDeleted).HasDefaultValue(false);
        builder.Property(u => u.DeletedAt).IsRequired(false);
        builder.Property(u => u.DeletedByUserId).IsRequired(false);
        builder.HasIndex(u => u.IsDeleted);
    }
}
