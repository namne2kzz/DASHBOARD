using DASHBOARD.Domain.Entities;
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
