namespace DASHBOARD.Domain.Interfaces;

/// <summary>
/// Marks an entity as soft-deletable.
/// EF Core's global query filter automatically excludes rows where <see cref="IsDeleted"/> is <c>true</c>.
/// Use <c>.IgnoreQueryFilters()</c> to bypass (admin restore, audit queries).
/// </summary>
public interface ISoftDelete
{
    /// <summary>Gets or sets whether this record has been soft-deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was soft-deleted. Null while active.</summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>Gets or sets the ID of the user who performed the deletion. Null while active.</summary>
    Guid? DeletedByUserId { get; set; }
}
