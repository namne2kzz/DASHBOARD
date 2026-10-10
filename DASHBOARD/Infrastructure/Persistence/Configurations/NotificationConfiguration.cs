using DASHBOARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DASHBOARD.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Notification"/>.</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>Configures the Notifications table mapping.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(500);
        builder.Property(n => n.Url).HasMaxLength(1000);
        builder.Property(n => n.Type).IsRequired();

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Primary query: fetch unread notifications for a user, newest first.
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
    }
}
