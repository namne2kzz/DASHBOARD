using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Auth;
using DASHBOARD.Infrastructure.Email;
using DASHBOARD.Infrastructure.Identity;
using DASHBOARD.Infrastructure.Messaging.Consumers;
using DASHBOARD.Infrastructure.Persistence;
using DASHBOARD.Infrastructure.Services;
using DASHBOARD.Infrastructure.Services.GitHub;
using DASHBOARD.Infrastructure.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Infrastructure;

/// <summary>Extension methods for registering Infrastructure layer services into the DI container.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core, Unit of Work, JWT, password hashing, invitation, email,
    /// Google auth, MassTransit/RabbitMQ, Git connections config, and settings services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Settings — single source of truth for all config key names ────────
        // Instantiate eagerly so it can be used for MassTransit setup below.
        var appSettings = new AppSettings(configuration);
        services.AddSingleton<IAppSettings>(appSettings);

        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql
                    .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)
                    .CommandTimeout(30))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Git repository integration (config-based, no DB entity — see GitConnectionsOptions) ─
        services.Configure<GitConnectionsOptions>(configuration.GetSection(GitConnectionsOptions.SectionName));
        services.AddSingleton<IValidateOptions<GitConnectionsOptions>, GitConnectionsOptionsValidator>();
        services.AddOptions<GitConnectionsOptions>().ValidateOnStart();
        services.AddScoped<IGitHubIntegrationService, OctokitGitHubIntegrationService>();

        // ── Distributed cache (read-through cache for GetGitRepositoryOverviewQueryHandler) ──
        // Backed by real Redis (docker-compose "redis" service) so the cache is shared across app
        // instances, not per-process. GetGitRepositoryOverviewQueryHandler depends only on
        // IDistributedCache, so this is the only place that knows the actual cache backend.
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        // In-process fallback (no external dependency) — kept here, commented out, in case Redis
        // is ever unavailable/removed and a same-process stand-in is needed again. Swap back by
        // commenting out the AddStackExchangeRedisCache block above and uncommenting this line;
        // GetGitRepositoryOverviewQueryHandler needs no changes either way since it only depends
        // on the IDistributedCache abstraction. Known limitation of this fallback: the cache is
        // per-instance, not shared across horizontally scaled app instances.
        // services.AddDistributedMemoryCache();

        // ── Query cache (L1 in-process + L2 Redis, tag-based eviction) ────────
        // HybridCache layers its own in-memory L1 over the IDistributedCache registered above, so a
        // repeat hit costs no network round-trip, and it collapses concurrent misses for one key into
        // a single factory call — the stampede protection a bare IDistributedCache cannot give.
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                // L2 default. Every entry here is tag-evicted by its mutation handler, so the TTL is
                // only a backstop for an eviction that never arrived (dropped message, crash).
                Expiration = TimeSpan.FromMinutes(5),
                // L1 stays far shorter than L2: a local copy must not outlive an eviction performed
                // on another instance, which this process never sees.
                LocalCacheExpiration = TimeSpan.FromSeconds(20),
            };
        });
        services.AddSingleton<IQueryCache, HybridQueryCache>();

        // ── Auth ──────────────────────────────────────────────────────────────
        services.Configure<JwtSettings>(configuration.GetSection(nameof(JwtSettings)));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IInvitationTokenService, InvitationTokenService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // ── Domain services ───────────────────────────────────────────────────
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRequestUserContext, RequestUserContext>();
        services.AddScoped<IHistoryService, HistoryService>();

        // ── Email ─────────────────────────────────────────────────────────────
        services.AddScoped<IEmailService, EmailService>();

        // ── Object storage (MinIO / S3-compatible) ────────────────────────────
        services.AddScoped<IStorageService, MinioStorageService>();

        // ── HUB Chat integration ──────────────────────────────────────────────
        // Named HttpClient pre-configured with base URL + internal token.
        // Registered even when HubChatBaseUrl is empty — HubChannelService checks before calling.
        services.AddHttpClient("HubChat", (sp, client) =>
        {
            var s = sp.GetRequiredService<IAppSettings>();
            if (!string.IsNullOrWhiteSpace(s.HubChatBaseUrl))
                client.BaseAddress = new Uri(s.HubChatBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.Add("X-Internal-Token", s.HubChatInternalToken);
        })
        // Forwards X-Correlation-Id so HUB Chat logs under the same id as this request.
        .AddHttpMessageHandler<CorrelationIdForwardingHandler>();
        services.AddTransient<CorrelationIdForwardingHandler>();
        services.AddScoped<IHubChannelService, HubChannelService>();

        // ── NMate (AI support assistant, repo SUPPORT) ────────────────────────
        // Same shape as HubChat: named client with base URL + internal token, correlation id forwarded.
        // Deliberately NO retry/resilience handler: /chat is a non-idempotent streamed POST — retrying
        // mid-stream would show the user a second answer and spend the free-tier quota twice.
        services.AddHttpClient(NMateGateway.ClientName, (sp, client) =>
        {
            var s = sp.GetRequiredService<IAppSettings>();
            if (!string.IsNullOrWhiteSpace(s.NMateBaseUrl))
                client.BaseAddress = new Uri(s.NMateBaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("X-Internal-Token", s.NMateInternalToken);
            client.Timeout = s.NMateTimeout;
        })
        .AddHttpMessageHandler<CorrelationIdForwardingHandler>();
        services.AddScoped<INMateGateway, NMateGateway>();

        // ── Messaging (MassTransit + RabbitMQ) ───────────────────────────────
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<InvitationCreatedConsumer>();
            bus.AddConsumer<GitSyncConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(appSettings.RabbitMqHost, h =>
                {
                    h.Username(appSettings.RabbitMqUsername);
                    h.Password(appSettings.RabbitMqPassword);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
