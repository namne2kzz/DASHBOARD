using System.Diagnostics;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Application.SystemInfo.DTOs;
using DASHBOARD.Infrastructure.Settings;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.SystemInfo.Queries.GetAbout;

/// <summary>Handles <see cref="GetAboutQuery"/>: reports this build and probes every external dependency.</summary>
public sealed class GetAboutQueryHandler(
    IApplicationDbContext db,
    IDistributedCache     cache,
    IBus                  bus,
    IAppSettings          settings,
    IBuildInfoProvider    build,
    IHttpClientFactory    httpFactory,
    IOptionsMonitor<GitConnectionsOptions> gitConnections,
    ILogger<GetAboutQueryHandler> logger) : IRequestHandler<GetAboutQuery, AboutDto>
{
    /// <summary>
    /// How long a single probe may take before it is called unreachable. A dialog the user is
    /// waiting on must not hang on a dead host, and every probe runs concurrently, so the whole
    /// response is bounded by this one timeout rather than the sum of them.
    /// </summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private const string Healthy     = "healthy";
    private const string Unreachable = "unreachable";

    /// <summary>Shown for a dependency that is wired up but deliberately not probed.</summary>
    private const string Configured = "configured";

    /// <summary>
    /// Shown for an optional integration whose base URL is blank. Distinct from "unreachable":
    /// nothing is wrong, the feature is simply switched off in this deployment, and the dialog
    /// should not show a red dot for a service nobody asked for.
    /// </summary>
    private const string NotConfigured = "not-configured";

    /// <summary>Gathers assembly/runtime facts and the live status of each dependency.</summary>
    /// <param name="query">The about query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="AboutDto"/> describing this build and its dependencies.</returns>
    public async Task<AboutDto> Handle(GetAboutQuery query, CancellationToken ct)
    {
        // Probes run together: three dead hosts cost one timeout, not three.
        var probes = await Task.WhenAll(
            // No host shown: IApplicationDbContext deliberately exposes only Set<T>(), and the
            // connection string belongs to Infrastructure. The round-trip itself is the answer.
            ProbeAsync("SQL Server", "database", "Application database", ct,
                async token => await db.Set<Domain.Entities.User>().AsNoTracking().Take(1).AnyAsync(token)),

            ProbeAsync("Redis", "cache", "Distributed cache", ct,
                async token =>
                {
                    // A miss is a healthy answer: it still proves the round-trip worked.
                    await cache.GetAsync("about:probe", token);
                    return true;
                }),

            ProbeAsync("RabbitMQ", "messaging", settings.RabbitMqHost, ct,
                async token =>
                {
                    // GetSendEndpoint resolves and connects the broker host; it publishes nothing.
                    await bus.GetSendEndpoint(new Uri("queue:about-probe")).WaitAsync(token);
                    return true;
                }),

            // Both are optional integrations: a deployment may run without chat or without the
            // assistant, so a blank base URL is reported as "not configured", not as a failure.
            // Only liveness endpoints are called — never HUB's message APIs or NMate's /chat,
            // which is a non-idempotent streamed POST that would spend quota on every dialog open.
            ProbeHttpAsync("HUB Chat", "chat", "HubChat", settings.HubChatBaseUrl, "health", ct),
            ProbeHttpAsync("NMate AI", "ai", "NMate", settings.NMateBaseUrl, "health/live", ct),

            ProbeGitHubAsync(ct));

        var services = probes.ToList();
        services.Add(DescribeStorage());
        services.Add(DescribeSmtp());

        return new AboutDto(
            Application:   "NFlow Dashboard",
            Version:       build.Version,
            BuildNumber:   build.BuildNumber,
            FullVersion:   build.FullVersion,
            CommitHash:    build.CommitHash,
            BuildDate:     build.BuildDate,
            Environment:   Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            ServerTimeUtc: DateTime.UtcNow,
            Runtime:       Environment.Version.ToString(),
            Services:      services);
    }

    /// <summary>Reports how many Git connections are configured and whether GitHub itself answers.</summary>
    /// <remarks>
    /// Tokens are per-repository (see <c>GitConnectionsOptions</c>), so there is no single
    /// credential to test with. The probe therefore calls the unauthenticated
    /// <c>/rate_limit</c> endpoint, which GitHub explicitly excludes from its own rate limit —
    /// it answers "is GitHub reachable from this host", not "is any given token still valid".
    /// </remarks>
    /// <param name="ct">Caller's cancellation token.</param>
    /// <returns>A populated <see cref="ServiceStatusDto"/>; never throws.</returns>
    private async Task<ServiceStatusDto> ProbeGitHubAsync(CancellationToken ct)
    {
        var connections = gitConnections.CurrentValue
            .Sum(repo => repo.Value?.Count ?? 0);

        if (connections == 0)
            return new ServiceStatusDto("GitHub", "git", NotConfigured, "No Git connections configured", null);

        var label = $"api.github.com · {connections} connection{(connections == 1 ? "" : "s")}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/rate_limit");
            // GitHub rejects requests without a User-Agent. A generic client name only — never
            // anything that identifies this deployment or its users to a third party.
            request.Headers.UserAgent.ParseAdd("nflow-dashboard-healthcheck");

            using var response = await client.SendAsync(request, timeout.Token);
            stopwatch.Stop();

            return response.IsSuccessStatusCode
                ? new ServiceStatusDto("GitHub", "git", Healthy, label, stopwatch.ElapsedMilliseconds)
                : new ServiceStatusDto("GitHub", "git", Unreachable,
                    $"{label} — HTTP {(int)response.StatusCode}", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (ct.IsCancellationRequested) throw;

            logger.LogWarning(ex, "About probe for GitHub failed.");

            var reason = timeout.IsCancellationRequested
                ? $"{label} — no response within {ProbeTimeout.TotalSeconds:0}s"
                : $"{label} — {ex.GetBaseException().Message}";

            return new ServiceStatusDto("GitHub", "git", Unreachable, reason, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Probes an optional HTTP integration by GETting its liveness endpoint.
    /// </summary>
    /// <remarks>
    /// A blank base URL means the integration is switched off in this deployment, which is
    /// reported as "not configured" rather than a failure. A non-success status still counts as
    /// reachable-but-unhealthy, so the reason shown is the status code rather than an exception.
    /// </remarks>
    /// <param name="name">Display name of the dependency.</param>
    /// <param name="kind">Stable machine key for the UI.</param>
    /// <param name="clientName">Named HttpClient to borrow (carries the internal token header).</param>
    /// <param name="baseUrl">Configured base URL; blank means the feature is off.</param>
    /// <param name="healthPath">Relative liveness path, e.g. "health".</param>
    /// <param name="ct">Caller's cancellation token.</param>
    /// <returns>A populated <see cref="ServiceStatusDto"/>; never throws.</returns>
    private async Task<ServiceStatusDto> ProbeHttpAsync(
        string name, string kind, string clientName, string baseUrl, string healthPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ServiceStatusDto(name, kind, NotConfigured, "Not configured in this deployment", null);

        var target = $"{baseUrl.TrimEnd('/')}/{healthPath}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = httpFactory.CreateClient(clientName);

            // Absolute URI: the named client's BaseAddress is configured for the integration's own
            // API root, which is not necessarily where the health endpoint lives.
            using var response = await client.GetAsync(target, timeout.Token);
            stopwatch.Stop();

            return response.IsSuccessStatusCode
                ? new ServiceStatusDto(name, kind, Healthy, baseUrl, stopwatch.ElapsedMilliseconds)
                : new ServiceStatusDto(name, kind, Unreachable,
                    $"{baseUrl} — HTTP {(int)response.StatusCode}", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (ct.IsCancellationRequested) throw;

            logger.LogWarning(ex, "About probe for {Service} failed.", name);

            var reason = timeout.IsCancellationRequested
                ? $"{baseUrl} — no response within {ProbeTimeout.TotalSeconds:0}s"
                : $"{baseUrl} — {ex.GetBaseException().Message}";

            return new ServiceStatusDto(name, kind, Unreachable, reason, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>Runs one probe under a timeout, turning any failure into an "unreachable" row.</summary>
    /// <param name="name">Display name of the dependency.</param>
    /// <param name="kind">Stable machine key for the UI.</param>
    /// <param name="detail">What to show when the probe succeeds.</param>
    /// <param name="ct">Caller's cancellation token.</param>
    /// <param name="probe">The probe itself; must not write any data.</param>
    /// <returns>A populated <see cref="ServiceStatusDto"/>; never throws.</returns>
    private async Task<ServiceStatusDto> ProbeAsync(
        string name, string kind, string detail, CancellationToken ct,
        Func<CancellationToken, Task<bool>> probe)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await probe(timeout.Token);
            stopwatch.Stop();
            return new ServiceStatusDto(name, kind, Healthy, detail, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // The caller going away is not a dependency failure — let it propagate.
            if (ct.IsCancellationRequested) throw;

            logger.LogWarning(ex, "About probe for {Service} failed.", name);

            var reason = timeout.IsCancellationRequested
                ? $"No response within {ProbeTimeout.TotalSeconds:0}s"
                : ex.GetBaseException().Message;

            return new ServiceStatusDto(name, kind, Unreachable, reason, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Reports the object store as configured rather than probed: <see cref="IStorageService"/>
    /// exposes only upload, delete and URL-building, so any real probe would either write an object
    /// or prove nothing. Showing the target without claiming it is reachable is the honest option.
    /// shortcut: configuration only, no connectivity check — upgrade when IStorageService gains a read.
    /// </summary>
    private ServiceStatusDto DescribeStorage() =>
        new("MinIO", "storage", Configured,
            $"{settings.MinioPublicBaseUrl} · bucket {settings.MinioAttachmentBucket}", null);

    /// <summary>
    /// Reports the mail host as configured. Opening an SMTP connection on every dialog open would
    /// be a login attempt against the mail server, which is not something a status panel should do.
    /// shortcut: configuration only, no connectivity check — upgrade if mail delivery needs monitoring.
    /// </summary>
    private ServiceStatusDto DescribeSmtp() =>
        new("SMTP", "email", Configured, $"{settings.SmtpHost}:{settings.SmtpPort}", null);

}
