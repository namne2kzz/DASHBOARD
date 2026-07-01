using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SmartBoardColumn"/>.</summary>
internal sealed class SmartBoardColumnConfiguration : IEntityTypeConfiguration<SmartBoardColumn>
{
    /// <summary>Configures the SmartBoardColumns table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<SmartBoardColumn> builder)
    {
        builder.ToTable("SmartBoardColumns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.WipLimit).HasDefaultValue(0);
        builder.Property(c => c.AgingLimitDays).HasDefaultValue(3);

        builder.HasIndex(c => new { c.RepositoryId, c.Order });
        builder.HasIndex(c => new { c.RepositoryId, c.MappedState });
    }
}
