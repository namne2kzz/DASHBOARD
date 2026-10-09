using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Sprint"/>.</summary>
internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    /// <summary>Configures the Sprints table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SprintStatus.Planning);
        builder.Property(s => s.ClosedAt);

        // Partial index: enforces at most one Active sprint per repository at the DB level.
        builder.HasIndex(s => new { s.RepositoryId, s.Status })
            .HasFilter($"\"Status\" = '{SprintStatus.Active}'")
            .IsUnique()
            .HasDatabaseName("IX_Sprints_Repository_OneActive");

        builder.HasMany(s => s.CapacityMembers)
            .WithOne(c => c.Sprint)
            .HasForeignKey(c => c.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.DaysOff)
            .WithOne(d => d.Sprint)
            .HasForeignKey(d => d.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Tasks)
            .WithOne(t => t.Sprint)
            .HasForeignKey(t => t.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.RepositoryId);
    }
}
