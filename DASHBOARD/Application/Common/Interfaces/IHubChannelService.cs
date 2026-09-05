namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Integrates with HUB Chat to manage channels linked to DASHBOARD sprints.
/// All methods are best-effort — they log failures and never throw to the caller,
/// so sprint operations succeed even when HUB is unavailable.
/// </summary>
public interface IHubChannelService
{
    /// <summary>
    /// Finds or creates a Private HUB channel for the given sprint, then returns its channel ID.
    /// Returns <see langword="null"/> if the call to HUB fails or times out.
    /// </summary>
    /// <param name="workspaceId">DASHBOARD repository ID (= HUB workspace).</param>
    /// <param name="sprintId">Sprint ID used as the channel's link key.</param>
    /// <param name="sprintName">Sprint display name, used to derive the channel name.</param>
    /// <param name="creatorUserId">User creating the sprint — becomes channel owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HUB channel ID, or <see langword="null"/> on failure.</returns>
    Task<Guid?> FindOrCreateSprintChannelAsync(
        Guid workspaceId, Guid sprintId, string sprintName, Guid creatorUserId, CancellationToken ct);

    /// <summary>
    /// Adds a user to the sprint's linked HUB channel (capacity member sync). Best-effort.
    /// </summary>
    /// <param name="channelId">HUB channel ID.</param>
    /// <param name="userId">User to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddMemberAsync(Guid channelId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Removes a user from the sprint's linked HUB channel (capacity member removed). Best-effort.
    /// </summary>
    /// <param name="channelId">HUB channel ID.</param>
    /// <param name="userId">User to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveMemberAsync(Guid channelId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Archives the sprint's linked HUB channel when the sprint is closed. Best-effort.
    /// </summary>
    /// <param name="channelId">HUB channel ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ArchiveAsync(Guid channelId, CancellationToken ct);
}
