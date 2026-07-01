using System.Text.Json;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Role"/>.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    /// <summary>Configures the Roles table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsDefault).IsRequired();

        // AllowedFunctions serialized as JSON array (e.g. ["CreateWorkItem","EditWorkItem"]).
        builder.Property(r => r.AllowedFunctions)
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOpts),
                v => JsonSerializer.Deserialize<List<SystemFunction>>(v, JsonOpts) ?? new List<SystemFunction>())
            .HasColumnType("nvarchar(max)");

        // RepositoryId is null for global default roles, set for repository-scoped custom roles.
        builder.HasOne(r => r.Repository)
            .WithMany()
            .HasForeignKey(r => r.RepositoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
