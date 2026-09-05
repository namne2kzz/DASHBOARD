using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Organization"/>.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    /// <summary>Configures the Organizations table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);

        // Alias is the tenant prefix in URLs — unique system-wide.
        builder.Property(o => o.Alias).IsRequired().HasMaxLength(50);
        builder.HasIndex(o => o.Alias).IsUnique();

        builder.Property(o => o.ContactEmail).IsRequired().HasMaxLength(320);
        builder.Property(o => o.About).HasMaxLength(2000);

        // One license key binds to at most one organization.
        builder.Property(o => o.LicenseKey).IsRequired().HasMaxLength(500);
        builder.HasIndex(o => o.LicenseKey).IsUnique();

        builder.Property(o => o.LicenseRepoCapacity).IsRequired();
    }
}
