using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DASHBOARD.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory against the SQL Server container, replacing only the
/// dependencies that reach outside the process.
/// </summary>
/// <remarks>
/// The point of these tests is to exercise the genuine request pipeline — routing, model binding,
/// authentication, authorisation filters, MediatR behaviours, EF Core against SQL Server. So the
/// substitutions are kept to the minimum: message broker, cache, object storage and the HUB client,
/// none of which are available in a test run and none of which these tests are about.
/// </remarks>
public sealed class ApiFactory(SqlServerFixture database) : WebApplicationFactory<Program>
{
    /// <summary>The shared token the <c>/internal</c> routes expect during tests.</summary>
    public const string InternalToken = "integration-test-internal-token";

    /// <summary>Configuration the test host runs with.</summary>
    private Dictionary<string, string?> HostSettings => new()
    {
        ["ConnectionStrings:Default"] = database.ConnectionString,

        // A 32-byte key is the minimum HS256 accepts; the value itself is irrelevant because
        // tests mint their tokens through the real login endpoint.
        ["JwtSettings:Secret"]   = "integration-test-signing-key-32-bytes!",
        ["JwtSettings:Issuer"]   = "dashboard-api",
        ["JwtSettings:Audience"] = "dashboard-spa",

        ["Invitation:FrontendBaseUrl"] = "http://localhost:4200",

        // The /internal routes are AllowAnonymous but sit behind a shared-token middleware;
        // tests need the expected value in order to exercise both branches.
        ["InternalApi:Token"] = InternalToken,
    };

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs reads some settings straight off builder.Configuration while composing the
        // host — notably JwtSettings:Secret, which it bakes into the bearer IssuerSigningKey. That
        // read happens before ConfigureAppConfiguration below runs, so those keys have to arrive
        // through UseSetting instead. Supplying the secret only via ConfigureAppConfiguration left
        // tokens signed with the test key but validated against the appsettings one, and every
        // authenticated request came back 401 "The signature key was not found".
        foreach (var (key, value) in HostSettings)
            builder.UseSetting(key, value);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Repeated here so anything resolving configuration at runtime sees the same values.
            config.AddInMemoryCollection(HostSettings);
        });

        builder.ConfigureServices(services =>
        {
            ReplaceDbContext(services, database.ConnectionString);
            RemoveMessageBroker(services);
            ReplaceDistributedCache(services);
            ReplaceExternalServices(services);
        });
    }

    private static void ReplaceDbContext(IServiceCollection services, string connectionString)
    {
        // Program.cs already registered a context from appsettings; point it at the container.
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<ApplicationDbContext>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Drops MassTransit entirely and puts a no-op publisher in its place.
    /// </summary>
    /// <remarks>
    /// The bus is a hosted service that dials RabbitMQ on start-up, so leaving it registered makes
    /// every test wait on a connection that will not arrive. Handlers only ever publish, so a stub
    /// <see cref="IPublishEndpoint"/> is enough; tests that care about a published message assert on
    /// <see cref="RecordingPublishEndpoint"/> instead.
    /// </remarks>
    private static void RemoveMessageBroker(IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true
                              || (d.ImplementationType?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) ?? false))
                     .ToList())
        {
            services.Remove(descriptor);
        }

        // AddMassTransit registers the bus as a hosted service; remove that too or start-up hangs.
        foreach (var hosted in services
                     .Where(d => d.ServiceType == typeof(IHostedService)
                              && d.ImplementationType?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true)
                     .ToList())
        {
            services.Remove(hosted);
        }

        services.AddSingleton<RecordingPublishEndpoint>();
        services.AddSingleton<IPublishEndpoint>(sp => sp.GetRequiredService<RecordingPublishEndpoint>());
    }

    private static void ReplaceDistributedCache(IServiceCollection services)
    {
        // Redis is not running in a test; an in-process cache keeps the same semantics.
        services.RemoveAll<IDistributedCache>();
        services.AddDistributedMemoryCache();
    }

    private static void ReplaceExternalServices(IServiceCollection services)
    {
        // Object storage and the HUB API are out of scope here and would otherwise make
        // outbound calls. Handlers treat both as best-effort already.
        services.RemoveAll<IStorageService>();
        services.AddSingleton<IStorageService, StubStorageService>();

        services.RemoveAll<IHubChannelService>();
        services.AddSingleton<IHubChannelService, StubHubChannelService>();

        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService, StubEmailService>();
    }
}
