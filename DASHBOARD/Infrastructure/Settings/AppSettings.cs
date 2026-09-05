using DASHBOARD.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DASHBOARD.Infrastructure.Settings;

/// <summary>
/// Reads all application configuration from <see cref="IConfiguration"/> through typed properties.
/// This is the single class that knows every configuration key name — rename a key in appsettings.json
/// and update only this file.
/// </summary>
public sealed class AppSettings : IAppSettings
{
    private readonly IConfiguration _config;

    /// <summary>Initialises the settings accessor with the application configuration.</summary>
    /// <param name="config">The application configuration root.</param>
    public AppSettings(IConfiguration config) => _config = config;

    // ── Invitation ───────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public TimeSpan InvitationTokenTtl =>
        TimeSpan.FromMinutes(_config.GetValue<int>("Invitation:TokenTtlMinutes"));

    /// <inheritdoc/>
    public string InvitationFrontendBaseUrl => Require("Invitation:FrontendBaseUrl");

    /// <inheritdoc/>
    public int InvitationRateLimitPermitLimit =>
        _config.GetValue<int>("Invitation:RateLimit:PermitLimit");

    /// <inheritdoc/>
    public TimeSpan InvitationRateLimitWindow =>
        TimeSpan.FromMinutes(_config.GetValue<int>("Invitation:RateLimit:WindowMinutes"));

    /// <inheritdoc/>
    public int InvitationRateLimitSegments =>
        _config.GetValue<int>("Invitation:RateLimit:SegmentsPerWindow");

    // ── Email / SMTP ─────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public string SmtpHost => Require("EmailSettings:SmtpHost");

    /// <inheritdoc/>
    public int SmtpPort => _config.GetValue<int>("EmailSettings:SmtpPort");

    /// <inheritdoc/>
    public string SmtpUsername => Require("EmailSettings:Username");

    /// <inheritdoc/>
    public string SmtpPassword => Require("EmailSettings:Password");

    /// <inheritdoc/>
    public string EmailFromAddress => Require("EmailSettings:FromEmail");

    // ── Google OAuth ─────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public string GoogleClientId => Require("Google:ClientId");

    // ── RabbitMQ ─────────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public string RabbitMqHost => Require("RabbitMq:Host");

    /// <inheritdoc/>
    public string RabbitMqUsername => Require("RabbitMq:Username");

    /// <inheritdoc/>
    public string RabbitMqPassword => Require("RabbitMq:Password");

    // ── UI Defaults ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public string DefaultGoogleUserAvatarClass => Require("Defaults:GoogleUserAvatarClass");

    // ── HUB Chat integration ─────────────────────────────────────────────────
    /// <inheritdoc/>
    public string HubChatBaseUrl => _config["HubChat:BaseUrl"] ?? string.Empty;

    /// <inheritdoc/>
    public string HubChatInternalToken => _config["HubChat:InternalToken"] ?? string.Empty;

    /// <inheritdoc/>
    public string HubFrontendBaseUrl => _config["HubChat:FrontendBaseUrl"] ?? string.Empty;

    /// <summary>Returns the config value for <paramref name="key"/> or throws if missing/empty.</summary>
    /// <param name="key">The configuration key path (e.g. "EmailSettings:SmtpHost").</param>
    /// <returns>The non-null, non-empty configuration value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is absent or empty in appsettings.</exception>
    private string Require(string key) =>
        _config[key] ?? throw new InvalidOperationException(
            $"Required configuration key '{key}' is missing or empty in appsettings.");
}
