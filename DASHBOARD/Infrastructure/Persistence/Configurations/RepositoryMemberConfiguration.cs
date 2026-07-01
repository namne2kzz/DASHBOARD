using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="RepositoryMember"/>.</summary>
internal sealed class RepositoryMemberConfiguration : IEntityTypeConfiguration<RepositoryMember>
{
    /// <summary>Configures the RepositoryMembers table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RepositoryMember> builder)
    {
        builder.ToTable("RepositoryMembers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.DefaultRole).IsRequired().HasMaxLength(100);

        // One user can belong to a repository only once.
        builder.HasIndex(m => new { m.UserId, m.RepositoryId }).IsUnique();

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Repository)
            .WithMany()
            .HasForeignKey(m => m.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Role)
            .WithMany()
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired();
    }
}
