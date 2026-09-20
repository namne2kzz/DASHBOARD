using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core table mapping for <see cref="UserSetting"/>.</summary>
internal sealed class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    /// <summary>Configures the UserSettings table.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(s => s.Id);

        // Key is a stable dot-separated token, e.g. "ui.date-format".
        builder.Property(s => s.Key).IsRequired().HasMaxLength(100);

        // Value is nullable — null means "use app default".
        builder.Property(s => s.Value).HasMaxLength(1000).IsRequired(false);

        // Enforce one value per key per user at the DB level.
        builder.HasIndex(s => new { s.UserId, s.Key })
               .IsUnique()
               .HasDatabaseName("IX_UserSettings_UserId_Key");

        // Index for bulk-load of all settings for one user (used on login).
        builder.HasIndex(s => s.UserId)
               .HasDatabaseName("IX_UserSettings_UserId");

        builder.HasOne(s => s.User)
               .WithMany()
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
