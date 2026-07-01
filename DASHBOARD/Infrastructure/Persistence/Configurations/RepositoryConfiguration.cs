using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Repository"/>.</summary>
internal sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    /// <summary>Configures the Repositories table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);

        // Code is the Jira-style project key (e.g. "DASH"). Max 10 chars, must be unique system-wide.
        builder.Property(r => r.Code).IsRequired().HasMaxLength(10);
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.IsArchived).HasDefaultValue(false);
        builder.HasIndex(r => r.IsArchived);

        builder.HasIndex(r => r.Code).IsUnique();
    }
}
