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

var builder = WebApplication.CreateBuilder(args);

// ── Layer services ────────────────────────────────────────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ── Controllers & OpenAPI ─────────────────────────────────────────────────────
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

// ── Health checks ─────────────────────────────────────────────────────────────
// /health       — liveness: process is up; never touches the DB (CD's deploy gate).
// /health/ready — readiness: also verifies the database connection.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

// ── JWT Authentication ────────────────────────────────────────────────────────
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

// ── Rate Limiting ─────────────────────────────────────────────────────────────
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

// ── CORS ──────────────────────────────────────────────────────────────────────
var frontendBaseUrl = builder.Configuration["Invitation:FrontendBaseUrl"] ?? "http://localhost:4200";

builder.Services.AddCors(opt =>
    opt.AddPolicy(AppConstants.AngularCorsPolicy, policy =>
        policy.WithOrigins(frontendBaseUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Startup: schema + object storage ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
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

// ── Middleware pipeline ───────────────────────────────────────────────────────
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
