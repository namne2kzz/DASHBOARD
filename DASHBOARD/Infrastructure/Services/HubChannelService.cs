using System.Net.Http.Json;
using DASHBOARD.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Calls HUB Chat's internal API to manage sprint-linked channels.
/// Uses <see cref="IHttpClientFactory"/> to avoid socket exhaustion.
/// All public methods swallow exceptions and log them — sprint operations
/// must succeed even when HUB is unavailable.
/// </summary>
/// <param name="factory">HTTP client factory (named client "HubChat" registered in DI).</param>
/// <param name="logger">Logger.</param>
public sealed class HubChannelService(
    IHttpClientFactory          factory,
    ILogger<HubChannelService>  logger)
    : IHubChannelService
{
    private HttpClient Client => factory.CreateClient("HubChat");

    /// <inheritdoc />
    public async Task<Guid?> FindOrCreateSprintChannelAsync(
        Guid workspaceId, Guid sprintId, string sprintName, Guid creatorUserId, CancellationToken ct)
    {
        try
        {
            var response = await Client.PostAsJsonAsync("/internal/channels/sprint", new
            {
                workspaceId,
                sprintId,
                sprintName,
                creatorUserId,
            }, ct);

            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<HubChannelDto>(ct);
            return dto?.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "HUB channel creation failed for sprint {SprintId} in workspace {WorkspaceId}. " +
                "Sprint was created without a linked channel.",
                sprintId, workspaceId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        try
        {
            var response = await Client.PostAsync(
                $"/internal/channels/{channelId}/members/{userId}", content: null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to add user {UserId} to HUB channel {ChannelId}.", userId, channelId);
        }
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        try
        {
            var response = await Client.DeleteAsync(
                $"/internal/channels/{channelId}/members/{userId}", ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to remove user {UserId} from HUB channel {ChannelId}.", userId, channelId);
        }
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(Guid channelId, CancellationToken ct)
    {
        try
        {
            var response = await Client.PostAsync(
                $"/internal/channels/{channelId}/archive", content: null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to archive HUB channel {ChannelId}.", channelId);
        }
    }

    // ── Local DTO ─────────────────────────────────────────────────────────────

    private sealed record HubChannelDto(Guid Id);
}
