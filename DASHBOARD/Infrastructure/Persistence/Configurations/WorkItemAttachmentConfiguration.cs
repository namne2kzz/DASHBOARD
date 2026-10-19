using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WorkItemAttachment"/>.</summary>
internal sealed class WorkItemAttachmentConfiguration : IEntityTypeConfiguration<WorkItemAttachment>
{
    /// <summary>Configures the WorkItemAttachments table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<WorkItemAttachment> builder)
    {
        builder.ToTable("WorkItemAttachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ObjectKey).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.PublicUrl).IsRequired().HasMaxLength(2000);

        builder.HasOne(a => a.SprintTask)
            .WithMany()
            .HasForeignKey(a => a.SprintTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.SprintTaskId)
            .HasDatabaseName("IX_WorkItemAttachments_SprintTaskId");
    }
}
