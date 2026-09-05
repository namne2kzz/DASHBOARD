namespace DASHBOARD.Domain.Entities;

/// <summary>
/// Tracks the HUB channel that was automatically created for a sprint.
/// One-to-one with <see cref="Sprint"/> — a sprint has at most one linked channel.
/// </summary>
public sealed class SprintChannelLink : Common.BaseEntity
{
    /// <summary>The sprint this channel was created for.</summary>
    public Guid SprintId { get; set; }

    /// <summary>The HUB channel ID (primary key of <c>hub_chat.Channels</c>).</summary>
    public Guid HubChannelId { get; set; }

    /// <summary>
    /// Deep-link URL into the HUB frontend, e.g. <c>http://localhost:4202/channels/{id}</c>.
    /// Stored so the UI can open HUB without knowing the URL pattern.
    /// </summary>
    public string HubChannelUrl { get; set; } = string.Empty;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>The linked sprint.</summary>
    public Sprint? Sprint { get; set; }
}
