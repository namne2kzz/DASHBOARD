using System.Text.Json;
using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="BacklogItem"/>.</summary>
internal sealed class BacklogItemConfiguration : IEntityTypeConfiguration<BacklogItem>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    /// <summary>Configures the BacklogItems table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<BacklogItem> builder)
    {
        builder.ToTable("BacklogItems");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(500);
        builder.Property(b => b.AcceptanceCriteria).HasColumnType("nvarchar(max)");
        builder.Property(b => b.Rank).HasPrecision(18, 8);

        builder.Property(b => b.Documents)
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOpts),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasOne(b => b.Sprint)
            .WithMany()
            .HasForeignKey(b => b.SprintId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(b => b.Parent)
            .WithMany(b => b.Children)
            .HasForeignKey(b => b.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(b => b.RepositoryId);
        builder.HasIndex(b => new { b.RepositoryId, b.Type, b.Rank });
    }
}
