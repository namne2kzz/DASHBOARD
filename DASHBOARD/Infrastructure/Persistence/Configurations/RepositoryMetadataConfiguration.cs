using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="RepositoryMetadata"/>.</summary>
internal sealed class RepositoryMetadataConfiguration : IEntityTypeConfiguration<RepositoryMetadata>
{
    /// <summary>Configures the RepositoryMetadata table, constraints, and relationships.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RepositoryMetadata> builder)
    {
        builder.ToTable("RepositoryMetadata");
        builder.HasKey(m => m.Id);

        // Store enum as string for readability and forward-compatibility.
        builder.Property(m => m.Key)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Value)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.IsGlobal).HasDefaultValue(false);

        builder.Property(m => m.IsDeleted).HasDefaultValue(false);
        builder.Property(m => m.DeletedAt).IsRequired(false);
        builder.Property(m => m.DeletedByUserId).IsRequired(false);
        builder.HasIndex(m => m.IsDeleted);

        // Duplicate values for the same key in the same repo are not allowed.
        builder.HasIndex(m => new { m.RepositoryId, m.Key, m.Value })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_RepositoryMetadata_RepoId_Key_Value_Active");

        // Index for the most common query pattern: repo + key.
        builder.HasIndex(m => new { m.RepositoryId, m.Key })
            .HasDatabaseName("IX_RepositoryMetadata_RepoId_Key");

        // RepositoryId is null for global catalog entries shared across all repositories.
        builder.HasOne(m => m.Repository)
            .WithMany()
            .HasForeignKey(m => m.RepositoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
