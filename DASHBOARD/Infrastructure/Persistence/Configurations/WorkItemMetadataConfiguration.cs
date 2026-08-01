using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WorkItemMetadata"/> — the work item ↔ metadata catalog join.</summary>
internal sealed class WorkItemMetadataConfiguration : IEntityTypeConfiguration<WorkItemMetadata>
{
    /// <summary>Configures the WorkItemMetadata table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<WorkItemMetadata> builder)
    {
        builder.ToTable("WorkItemMetadata");
        builder.HasKey(x => x.Id);

        // A work item can carry each catalog value at most once.
        builder.HasIndex(x => new { x.SprintTaskId, x.MetadataId }).IsUnique();
        builder.HasIndex(x => x.MetadataId);

        builder.HasOne(x => x.SprintTask)
            .WithMany()
            .HasForeignKey(x => x.SprintTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Metadata)
            .WithMany()
            .HasForeignKey(x => x.MetadataId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
