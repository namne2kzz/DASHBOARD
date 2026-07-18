namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Single access point for all application configuration values.
/// Centralises configuration key names so that a key rename in appsettings.json
/// requires a change in only one place — the implementing class.
/// </summary>
public interface IAppSettings
{
    // ── Invitation ───────────────────────────────────────────────────────────
    /// <summary>Gets how long an invitation token remains valid before it expires.</summary>
    TimeSpan InvitationTokenTtl { get; }

    /// <summary>
    /// Gets the frontend base URL used to build the accept link in invitation emails
    /// (e.g. "https://app.dashboard.dev"). Sourced from config — never from the request.
    /// </summary>
    string InvitationFrontendBaseUrl { get; }

    /// <summary>Gets the maximum number of accept-invitation requests allowed per IP per rate-limit window.</summary>
    int InvitationRateLimitPermitLimit { get; }

    /// <summary>Gets the sliding window duration for the accept-invitation rate limiter.</summary>
    TimeSpan InvitationRateLimitWindow { get; }

    /// <summary>Gets the number of segments the sliding window is divided into.</summary>
    int InvitationRateLimitSegments { get; }

    // ── Email / SMTP ─────────────────────────────────────────────────────────
    /// <summary>Gets the SMTP server hostname.</summary>
    string SmtpHost { get; }

    /// <summary>Gets the SMTP server port (typically 587 for STARTTLS).</summary>
    int SmtpPort { get; }

    /// <summary>Gets the SMTP login username.</summary>
    string SmtpUsername { get; }

    /// <summary>Gets the SMTP login password or app-specific password.</summary>
    string SmtpPassword { get; }

    /// <summary>Gets the From email address.</summary>
    string EmailFromAddress { get; }

    // ── Google OAuth ─────────────────────────────────────────────────────────
    /// <summary>Gets the Google OAuth 2.0 Client ID used to verify id_tokens.</summary>
    string GoogleClientId { get; }

    // ── RabbitMQ ─────────────────────────────────────────────────────────────
    /// <summary>Gets the RabbitMQ broker hostname.</summary>
    string RabbitMqHost { get; }

    /// <summary>Gets the RabbitMQ login username.</summary>
    string RabbitMqUsername { get; }

    /// <summary>Gets the RabbitMQ login password.</summary>
    string RabbitMqPassword { get; }

    // ── UI Defaults ──────────────────────────────────────────────────────────
    /// <summary>Gets the default Tailwind avatar CSS class applied to new Google-authenticated users.</summary>
    string DefaultGoogleUserAvatarClass { get; }
}
