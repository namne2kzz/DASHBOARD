using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="HistoryEntry"/>.</summary>
internal sealed class HistoryEntryConfiguration : IEntityTypeConfiguration<HistoryEntry>
{
    /// <summary>Configures the HistoryEntries table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<HistoryEntry> builder)
    {
        builder.ToTable("HistoryEntries");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Message).IsRequired().HasMaxLength(1000);

        builder.HasOne(h => h.SprintTask)
            .WithMany()
            .HasForeignKey(h => h.SprintTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Author)
            .WithMany()
            .HasForeignKey(h => h.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.SprintTaskId);
        builder.HasIndex(h => h.RepositoryId);
    }
}
