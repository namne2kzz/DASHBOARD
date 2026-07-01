using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SprintTask"/>.</summary>
internal sealed class SprintTaskConfiguration : IEntityTypeConfiguration<SprintTask>
{
    /// <summary>Configures the SprintTasks table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<SprintTask> builder)
    {
        builder.ToTable("SprintTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).HasMaxLength(10_000).HasDefaultValue(string.Empty);
        builder.Property(t => t.OriginalEstimate).HasPrecision(8, 2);
        builder.Property(t => t.RemainingWork).HasPrecision(8, 2);
        builder.Property(t => t.CompletedWork).HasPrecision(8, 2);

        // Bug fields — optional, max lengths guard against runaway text
        builder.Property(t => t.StepsToReproduce).HasMaxLength(5_000);
        builder.Property(t => t.Environment).HasMaxLength(500);
        builder.Property(t => t.RootCause).HasMaxLength(2_000);
        builder.Property(t => t.Solution).HasMaxLength(2_000);
        builder.Property(t => t.Impaction).HasMaxLength(1_000);
        builder.Property(t => t.UnitTest).HasMaxLength(2_000);
        builder.Property(t => t.DesignReview).HasMaxLength(2_000);

        // TestPlan — store test steps as JSON array
        builder.Property(t => t.TestSteps)
            .HasConversion(
                v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null))
            .HasMaxLength(20_000);

        // SprintId is nullable — Bug/TestPlan can exist without a sprint
        builder.HasOne(t => t.Sprint)
            .WithMany(s => s.Tasks)
            .HasForeignKey(t => t.SprintId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(t => t.Parent)
            .WithMany(t => t.SubTasks)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(t => t.BacklogItem)
            .WithMany()
            .HasForeignKey(t => t.BacklogItemId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Discussions are configured on DiscussionEntryConfiguration (SprintTaskId FK).

        // Soft delete filter
        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.SprintId);
        builder.HasIndex(t => t.RepositoryId);
        builder.HasIndex(t => new { t.RepositoryId, t.WorkItemNumber }).IsUnique();
        builder.HasIndex(t => new { t.RepositoryId, t.Type, t.State });
    }
}
