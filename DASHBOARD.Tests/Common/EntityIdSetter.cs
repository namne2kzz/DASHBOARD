using DASHBOARD.Domain.Common;

namespace DASHBOARD.Tests.Common;

/// <summary>Assigns a specific <see cref="BaseEntity.Id"/> to an entity in tests.</summary>
/// <remarks>
/// <see cref="BaseEntity.Id"/> has a protected setter and self-assigns a new GUID, which is the right
/// design for production code. Tests occasionally need a known id — typically when a handler is given
/// a repository id up front and then looks the row up by it — so this reaches the setter through
/// reflection in one place rather than repeating it across test fixtures.
/// </remarks>
public static class EntityIdSetter
{
    /// <summary>Overwrites the entity's identifier and returns the same instance.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to stamp.</param>
    /// <param name="id">The identifier to assign.</param>
    /// <returns>The same entity, for chaining.</returns>
    public static TEntity WithId<TEntity>(this TEntity entity, Guid id) where TEntity : BaseEntity
    {
        typeof(BaseEntity)
            .GetProperty(nameof(BaseEntity.Id))!
            .SetValue(entity, id);

        return entity;
    }
}
