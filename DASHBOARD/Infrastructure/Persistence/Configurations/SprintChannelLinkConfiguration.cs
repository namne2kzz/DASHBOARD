using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SprintChannelLink"/>.</summary>
internal sealed class SprintChannelLinkConfiguration : IEntityTypeConfiguration<SprintChannelLink>
{
    /// <summary>Configures the SprintChannelLinks table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<SprintChannelLink> builder)
    {
        builder.ToTable("SprintChannelLinks");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.HubChannelUrl).IsRequired().HasMaxLength(500);

        // One-to-one: one sprint → at most one channel link
        builder.HasOne(l => l.Sprint)
            .WithOne()
            .HasForeignKey<SprintChannelLink>(l => l.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index on SprintId enforces 1-to-1 at DB level
        builder.HasIndex(l => l.SprintId).IsUnique();

        // Index on HubChannelId for reverse lookups (e.g. archive on sprint close)
        builder.HasIndex(l => l.HubChannelId);
    }
}
