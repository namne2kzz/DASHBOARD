using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WorkItemLink"/>.</summary>
internal sealed class WorkItemLinkConfiguration : IEntityTypeConfiguration<WorkItemLink>
{
    /// <summary>Configures the WorkItemLinks table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<WorkItemLink> builder)
    {
        builder.ToTable("WorkItemLinks");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LinkType).HasConversion<int>();
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.HasOne(l => l.SourceTask)
            .WithMany()
            .HasForeignKey(l => l.SourceTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade on the target side — a target can be deleted independently; the row is cleaned up
        // by the delete handler. Cascade on both FKs would create multiple cascade paths (SQL Server rejects).
        builder.HasOne(l => l.TargetTask)
            .WithMany()
            .HasForeignKey(l => l.TargetTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.CreatedBy)
            .WithMany()
            .HasForeignKey(l => l.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // One link of a given type between the same ordered pair.
        builder.HasIndex(l => new { l.SourceTaskId, l.TargetTaskId, l.LinkType })
            .IsUnique()
            .HasDatabaseName("IX_WorkItemLinks_Source_Target_Type");

        builder.HasIndex(l => l.SourceTaskId).HasDatabaseName("IX_WorkItemLinks_SourceTaskId");
        builder.HasIndex(l => l.TargetTaskId).HasDatabaseName("IX_WorkItemLinks_TargetTaskId");
    }
}
