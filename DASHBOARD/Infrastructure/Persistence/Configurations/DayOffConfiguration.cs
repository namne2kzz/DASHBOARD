using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="DayOff"/>.</summary>
internal sealed class DayOffConfiguration : IEntityTypeConfiguration<DayOff>
{
    /// <summary>Configures the DaysOff table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DayOff> builder)
    {
        builder.ToTable("DaysOff");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Hours).HasPrecision(4, 1);
        builder.Property(d => d.Reason).HasMaxLength(500);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(d => d.SprintId);
        builder.HasIndex(d => d.RepositoryId);
    }
}
