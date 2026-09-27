using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DASHBOARD.Application;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Core.Constants;
using DASHBOARD.Infrastructure;
using DASHBOARD.Infrastructure.Persistence;
using DASHBOARD.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger ─────────────────────────────────────────────────────────
// Replaced by the configured logger once the host is built. It exists so that a crash
// *before* that point — a missing JwtSettings:Secret, an unreachable database during
// MigrateAsync, MinIO refusing the bucket call — lands in a file instead of vanishing.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bootstrap-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging (Serilog) ─────────────────────────────────────────────────────
    // Sinks, levels and output templates all come from the "Serilog" section of
    // appsettings*.json — see docs/LOGGING-DESIGN.md. Nothing is hardcoded here so
    // that per-environment overrides need no code change.
    builder.Host.UseSerilog((context, services, cfg) => cfg
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "DASHBOARD.Api"));

    // ── Layer services ────────────────────────────────────────────────────────
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ── Controllers & OpenAPI ─────────────────────────────────────────────────
    builder.Services.AddControllers();

    builder.Services.AddApiVersioning(opt =>
    {
        opt.ReportApiVersions                  = true;
        opt.DefaultApiVersion                  = new ApiVersion(1, 0);
        opt.AssumeDefaultVersionWhenUnspecified = true;
        // URL segment only: /api/v1/... — simple, cacheable, no header gymnastics.
        opt.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(opt =>
    {
        opt.GroupNameFormat           = "'v'VVV";   // → "v1", "v2"
        opt.SubstituteApiVersionInUrl = true;
    });

    builder.Services.AddOpenApi();

    // ── Health checks ─────────────────────────────────────────────────────────
    // /health       — liveness: process is up; never touches the DB (CD's deploy gate).
    // /health/ready — readiness: also verifies the database connection.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

    // ── JWT Authentication ────────────────────────────────────────────────────
    var jwtSecret = builder.Configuration["JwtSettings:Secret"]
        ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        });

    builder.Services.AddAuthorization();

    // ── Rate Limiting ─────────────────────────────────────────────────────────
    var rl = new DASHBOARD.Infrastructure.Settings.AppSettings(builder.Configuration);
    builder.Services.AddRateLimiter(opt =>
    {
        // Sliding window — prevents boundary-burst attacks that fixed windows allow.
        // Applied on AcceptInvitation endpoint via [EnableRateLimiting("accept-invitation")].
        opt.AddPolicy("accept-invitation", httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit          = rl.InvitationRateLimitPermitLimit,
                    Window               = rl.InvitationRateLimitWindow,
                    SegmentsPerWindow    = rl.InvitationRateLimitSegments,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit           = 0,
                }));

        opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ── CORS ──────────────────────────────────────────────────────────────────
    var frontendBaseUrl = builder.Configuration["Invitation:FrontendBaseUrl"] ?? "http://localhost:4200";

    builder.Services.AddCors(opt =>
        opt.AddPolicy(AppConstants.AngularCorsPolicy, policy =>
            policy.WithOrigins(frontendBaseUrl)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  // Lets the SPA read back the id the server actually used, so a user-reported
                  // error can be traced to its log lines.
                  .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Startup: schema + object storage ─────────────────────────────────────
    // await using, not using: the scope resolves MinioStorageService, which implements only
    // IAsyncDisposable — synchronous disposal of the scope throws.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        // Apply pending EF migrations so a fresh deploy targets the right schema.
        // Matches HUB's chat/media/notification services. Single-instance assumption:
        // if this ever scales past one replica, move migrations to a release step.
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var storage  = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var settings = scope.ServiceProvider.GetRequiredService<IAppSettings>();
        await storage.EnsureBucketExistsAsync(settings.MinioAvatarBucket, CancellationToken.None);
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    // First in the pipeline: every later log line, including the request summary and
    // any unhandled exception, should carry the correlation id.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Before ExceptionHandlingMiddleware so a request that ends in a 500 still gets one
    // complete HTTP summary line with its status and elapsed time.
    app.UseSerilogRequestLogging(opt =>
    {
        opt.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0000}ms";

        // Failures outrank everything; after that the noise check comes before the slow-request
        // rule, so a cold-start probe taking >1s does not get promoted to a Warning every boot.
        opt.GetLevel = (httpContext, elapsedMs, ex) =>
            ex is not null                              ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 500    ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400    ? LogEventLevel.Warning
            : IsNoisyPath(httpContext.Request.Path)     ? LogEventLevel.Verbose
            : elapsedMs > 1000                          ? LogEventLevel.Warning
                                                        : LogEventLevel.Information;

        opt.EnrichDiagnosticContext = (diagnostic, httpContext) =>
        {
            diagnostic.Set("RequestHost", httpContext.Request.Host.Value);
            diagnostic.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? "anonymous");
            diagnostic.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
        };
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // Must run before UseAuthentication so /internal/* is blocked without touching JWT.
    app.UseMiddleware<InternalApiKeyMiddleware>();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseHttpsRedirection();
    app.UseCors(AppConstants.AngularCorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();
    // Must run after UseAuthorization: an unauthenticated request must get its 401 rather than a 304
    // built from the ETag of some other user's cached payload.
    app.UseMiddleware<ETagMiddleware>();
    app.UseRateLimiter();

    // Health endpoints — intentionally unauthenticated so CD and load balancers can poll them.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        // Liveness: skip every registered check; only confirm the process responds.
        Predicate = _ => false,
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    app.MapControllers();

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "DASHBOARD API terminated unexpectedly during startup");
    return 1;
}
finally
{
    // Flushes buffered events; without it a shutdown can drop the last writes.
    Log.CloseAndFlush();
}

// Health and API-doc endpoints are polled constantly (every 10s by Docker/CD), which would
// otherwise bury real traffic in the log file. Demoted to Verbose rather than dropped so they
// can still be switched on when debugging a probe itself.
static bool IsNoisyPath(PathString path) =>
    path.StartsWithSegments("/health")
    || path.StartsWithSegments("/openapi")
    || path.StartsWithSegments("/swagger");
