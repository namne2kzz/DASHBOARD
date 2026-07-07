using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DASHBOARD.Application.Contracts;
using DASHBOARD.Application.GitRepositories;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Settings;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Controllers.Webhooks;

/// <summary>
/// Receives GitHub webhook deliveries for a project's configured connection(s) and, once verified,
/// publishes a <see cref="GitSyncRequestedEvent"/> so <c>GitSyncConsumer</c> can invalidate the
/// project's cached overview ahead of its normal ~60s TTL. No mirror tables are written here —
/// this endpoint only triggers a cache invalidation; the next Repos-tab request re-pulls live data
/// from GitHub via <c>GetGitRepositoryOverviewQueryHandler</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AllowAnonymousAttribute"/> is applied because GitHub cannot authenticate with this
/// app's JWT scheme. The caller is instead authenticated by the <c>X-Hub-Signature-256</c> HMAC
/// check in <see cref="Receive"/> — a request that fails signature verification is rejected with
/// 401 regardless of the (lack of) JWT, so this endpoint is not actually open to arbitrary callers.
/// </para>
/// </remarks>
[ApiController]
[Route("api/webhooks/github")]
[AllowAnonymous]
public sealed class GitHubWebhookController(
    IApplicationDbContext db,
    IOptionsMonitor<GitConnectionsOptions> gitConnections,
    IPublishEndpoint publisher,
    ILogger<GitHubWebhookController> logger) : ControllerBase
{
    /// <summary>GitHub event types that should trigger a cache-invalidation sync.</summary>
    private static readonly HashSet<string> SyncTriggeringEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "push",
        "pull_request",
    };

    /// <summary>
    /// Receives a single GitHub webhook delivery for the project identified by <paramref name="repositoryId"/>.
    /// Verifies the delivery's HMAC-SHA256 signature against the matching connection's configured
    /// <c>WebhookSecret</c> before acting on it; on a <c>push</c>/<c>pull_request</c> event, publishes a
    /// <see cref="GitSyncRequestedEvent"/> to invalidate the cached overview. Any other verified event
    /// type (e.g. GitHub's <c>ping</c> sent when the webhook is first created) is acknowledged without
    /// publishing anything.
    /// </summary>
    /// <param name="repositoryId">The project (repository) ID this webhook route was registered against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK for any signature-verified delivery (whether or not it was acted on) — GitHub disables a
    /// webhook after repeated non-2xx responses, so only a genuine signature failure returns 401.
    /// </returns>
    [HttpPost("{repositoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(Guid repositoryId, CancellationToken ct)
    {
        // Read the raw body bytes ourselves — this action takes no [FromBody] parameter, so nothing
        // else consumes Request.Body first. The HMAC must be computed over these exact bytes, not a
        // re-serialized/deserialized model, or a legitimate signature would fail to verify.
        byte[] rawBody;
        await using (var buffer = new MemoryStream())
        {
            await Request.Body.CopyToAsync(buffer, ct);
            rawBody = buffer.ToArray();
        }

        var repo = await db.Set<Repository>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repositoryId && !r.IsArchived, ct);

        if (repo is null || !gitConnections.CurrentValue.TryGetValue(repo.Code, out var entries) || entries.Count == 0)
        {
            logger.LogWarning("Rejected GitHub webhook for repository {RepositoryId}: no configured connection.", repositoryId);
            return Unauthorized();
        }

        var fullName = TryGetRepositoryFullName(rawBody);
        if (fullName is null)
        {
            logger.LogWarning("Rejected GitHub webhook for repository {RepositoryId}: payload has no repository.full_name.", repositoryId);
            return Unauthorized();
        }

        var matchedEntry = entries.FirstOrDefault(e =>
        {
            var (owner, name) = GitRepoUrlParser.Parse(e.RepoUrl);
            return string.Equals($"{owner}/{name}", fullName, StringComparison.OrdinalIgnoreCase);
        });

        if (matchedEntry is null || string.IsNullOrWhiteSpace(matchedEntry.WebhookSecret))
        {
            logger.LogWarning(
                "Rejected GitHub webhook for repository {RepositoryId}: no connection matches full_name {FullName} or it has no WebhookSecret configured.",
                repositoryId, fullName);
            return Unauthorized();
        }

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrEmpty(signatureHeader) || !VerifySignature(rawBody, matchedEntry.WebhookSecret, signatureHeader))
        {
            logger.LogWarning("Rejected GitHub webhook for repository {RepositoryId}: signature verification failed.", repositoryId);
            return Unauthorized();
        }

        var eventType = Request.Headers["X-GitHub-Event"].ToString();
        if (SyncTriggeringEvents.Contains(eventType))
        {
            await publisher.Publish(new GitSyncRequestedEvent(repositoryId, matchedEntry.RepoUrl), ct);
        }

        // Always 200 for a verified delivery, whether or not it was acted on — GitHub auto-disables
        // webhooks after repeated non-2xx responses, so ignored event types must still succeed.
        return Ok();
    }

    /// <summary>
    /// Extracts <c>repository.full_name</c> (e.g. <c>owner/repo</c>) from a raw webhook payload without
    /// binding the full GitHub webhook schema — every event type GitHub sends to a repo-scoped webhook
    /// includes this field, so a minimal <see cref="JsonDocument"/> read is enough to disambiguate which
    /// configured connection the delivery belongs to.
    /// </summary>
    /// <param name="rawBody">The raw webhook request body bytes.</param>
    /// <returns>The <c>owner/repo</c> full name, or <c>null</c> if the payload is malformed or lacks the field.</returns>
    private static string? TryGetRepositoryFullName(byte[] rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            return document.RootElement.TryGetProperty("repository", out var repository) &&
                   repository.TryGetProperty("full_name", out var fullName)
                ? fullName.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Verifies GitHub's <c>X-Hub-Signature-256</c> header against an HMAC-SHA256 of the raw body
    /// computed with the connection's <c>WebhookSecret</c>, using a constant-time comparison.
    /// </summary>
    /// <param name="rawBody">The raw webhook request body bytes the signature was computed over.</param>
    /// <param name="secret">The configured connection's <c>WebhookSecret</c>.</param>
    /// <param name="signatureHeader">The raw <c>X-Hub-Signature-256</c> header value (e.g. <c>sha256=&lt;hex&gt;</c>).</param>
    /// <returns><c>true</c> if the signature is valid; otherwise <c>false</c>.</returns>
    private static bool VerifySignature(byte[] rawBody, string secret, string signatureHeader)
    {
        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(signatureHeader[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(rawBody);

        return CryptographicOperations.FixedTimeEquals(computedHash, providedHash);
    }
}
