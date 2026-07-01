namespace DASHBOARD.Domain.Common;

/// <summary>Base class for all domain entities providing identity, audit timestamps, and soft-delete support.</summary>
public abstract class BaseEntity
{
    /// <summary>Gets the unique identifier for this entity.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>Gets the UTC timestamp when this entity was created.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>Gets the UTC timestamp of the last update; null if never updated.</summary>
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Stamps <see cref="UpdatedAt"/> with the current UTC time.</summary>
    public void Touch() => UpdatedAt = DateTime.UtcNow;
}
