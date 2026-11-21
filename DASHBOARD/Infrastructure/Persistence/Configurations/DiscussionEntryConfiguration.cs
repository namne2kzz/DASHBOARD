using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="DiscussionEntry"/>.</summary>
internal sealed class DiscussionEntryConfiguration : IEntityTypeConfiguration<DiscussionEntry>
{
    /// <summary>Configures the DiscussionEntries table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DiscussionEntry> builder)
    {
        builder.ToTable("DiscussionEntries");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(d => d.IsDeleted).HasDefaultValue(false);
        builder.Property(d => d.DeletedAt).IsRequired(false);
        builder.Property(d => d.DeletedByUserId).IsRequired(false);

        builder.HasOne(d => d.SprintTask)
            .WithMany(t => t.Discussions)
            .HasForeignKey(d => d.SprintTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Author)
            .WithMany()
            .HasForeignKey(d => d.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not Cascade: SQL Server refuses a self-referencing cascade (multiple
        // cascade paths), and replies are removed explicitly with their root anyway.
        builder.HasOne(d => d.Parent)
            .WithMany(d => d.Replies)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.SprintTaskId);
        builder.HasIndex(d => d.RepositoryId);

        // The thread query filters roots (ParentId == null) and counts/loads replies per root.
        builder.HasIndex(d => new { d.SprintTaskId, d.ParentId });
    }
}
