using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WikiPage"/>.</summary>
internal sealed class WikiPageConfiguration : IEntityTypeConfiguration<WikiPage>
{
    /// <summary>Configures the WikiPages table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<WikiPage> builder)
    {
        builder.ToTable("WikiPages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Content).HasColumnType("nvarchar(max)");

        builder.HasOne(p => p.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(p => p.RepositoryId);
        builder.HasIndex(p => new { p.RepositoryId, p.ParentId });

        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Property(p => p.DeletedAt).IsRequired(false);
        builder.Property(p => p.DeletedByUserId).IsRequired(false);
        builder.HasIndex(p => p.IsDeleted);
    }
}
