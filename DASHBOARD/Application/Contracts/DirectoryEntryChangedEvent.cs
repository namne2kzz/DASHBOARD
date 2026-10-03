// Namespace deliberately neutral (not DASHBOARD.*): HUB declares an identical copy under this same
// namespace, and MassTransit routes by message type name rather than by assembly. See
// MemberDirectoryChangedEvent for the same arrangement.
namespace Shared.IntegrationEvents;

/// <summary>Which kind of directory entry a <see cref="DirectoryEntryChangedEvent"/> invalidates.</summary>
public enum DirectoryEntryKind
{
    /// <summary>A user's profile — display name, avatar, admin flag or active state.</summary>
    UserProfile = 0,

    /// <summary>A user's display-preference settings.</summary>
    UserSettings = 1,

    /// <summary>A work item's linkable context — key, title or state.</summary>
    WorkItem = 2,
}

/// <summary>
/// Published whenever DASHBOARD changes something HUB's <c>DashboardGateway</c> caches, so the gateway
/// drops that one entry instead of serving it until its TTL expires.
/// </summary>
/// <remarks>
/// <para>
/// One message type carrying a <see cref="DirectoryEntryKind"/> rather than one type per entity: the
/// consumer does the same thing in every case (evict one key), so separate types would multiply
/// endpoints and cross-system contracts to keep in sync without changing any behaviour.
/// </para>
/// <para>
/// Membership changes keep their own <see cref="MemberDirectoryChangedEvent"/> — that one evicts two
/// related keys at once and carries both ids, so it is genuinely a different shape.
/// </para>
/// <para>
/// Carries no payload beyond the identity: HUB re-pulls from <c>/internal</c> on the next miss rather
/// than trusting values from the event, which keeps DASHBOARD free to reshape its DTOs.
/// </para>
/// </remarks>
/// <param name="Kind">Which cached entry went stale.</param>
/// <param name="EntityId">Id of the user or work item the entry belongs to.</param>
public sealed record DirectoryEntryChangedEvent(DirectoryEntryKind Kind, Guid EntityId);
