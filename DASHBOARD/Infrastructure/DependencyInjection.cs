using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Auth;
using DASHBOARD.Infrastructure.Email;
using DASHBOARD.Infrastructure.Identity;
using DASHBOARD.Infrastructure.Messaging.Consumers;
using DASHBOARD.Infrastructure.Persistence;
using DASHBOARD.Infrastructure.Services;
using DASHBOARD.Infrastructure.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DASHBOARD.Infrastructure;

/// <summary>Extension methods for registering Infrastructure layer services into the DI container.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core, Unit of Work, JWT, password hashing, invitation, email,
    /// Google auth, MassTransit/RabbitMQ, and settings services.
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

        // ── Messaging (MassTransit + RabbitMQ) ───────────────────────────────
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<InvitationCreatedConsumer>();

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
