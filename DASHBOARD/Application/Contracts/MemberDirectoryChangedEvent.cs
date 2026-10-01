// The namespace is deliberately neutral (not DASHBOARD.*): HUB declares an identical record under this
// same namespace, and MassTransit routes by message type name rather than by assembly. A namespace
// owned by neither system keeps that shared name from looking like HUB depends on DASHBOARD's layers.
namespace Shared.IntegrationEvents;

/// <summary>
/// Published whenever a repository's membership changes, so HUB's <c>DashboardGateway</c> can drop its
/// cached copy of that repository's directory instead of serving it until the TTL expires.
/// </summary>
/// <remarks>
/// <para>
/// Cross-system contract — HUB carries a byte-identical copy. Renaming or moving either copy silently
/// stops delivery, so change both together or version the message.
/// </para>
/// <para>
/// Cache invalidation is the only purpose, so the payload is deliberately minimal: HUB re-pulls the
/// authoritative list from <c>/internal</c> on the next miss rather than trusting anything carried
/// here. That keeps DASHBOARD free to change its member DTO without breaking HUB.
/// </para>
/// </remarks>
/// <param name="RepositoryId">The repository whose membership changed.</param>
/// <param name="UserId">The affected user, so HUB can also drop that user's cached memberships.</param>
public sealed record MemberDirectoryChangedEvent(Guid RepositoryId, Guid UserId);
