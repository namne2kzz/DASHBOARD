using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="CapacityMember"/>.</summary>
internal sealed class CapacityMemberConfiguration : IEntityTypeConfiguration<CapacityMember>
{
    /// <summary>Configures the CapacityMembers table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<CapacityMember> builder)
    {
        builder.ToTable("CapacityMembers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Role).IsRequired().HasMaxLength(100);
        builder.Property(c => c.HoursPerDay).HasPrecision(4, 1);
        builder.Property(c => c.OvertimeHoursPerDay).HasPrecision(4, 1);

        // One user can have only one capacity row per sprint.
        builder.HasIndex(c => new { c.SprintId, c.UserId }).IsUnique();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.RepositoryId);
    }
}
