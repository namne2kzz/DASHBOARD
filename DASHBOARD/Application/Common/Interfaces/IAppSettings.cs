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

    // ── Organisation provisioning (NKey) ─────────────────────────────────────
    /// <summary>
    /// Gets how long a password-setup token stays valid after an organisation is provisioned.
    /// Longer than an invitation token because onboarding often waits on the customer's schedule.
    /// </summary>
    TimeSpan PasswordSetupTokenTtl { get; }

    // ── Organisation alias lookup (sign-in step 1) ───────────────────────────
    /// <summary>
    /// Gets how many alias lookups one IP may make per <see cref="OrgLookupRateLimitWindow"/>.
    /// Deliberately small: the endpoint reveals whether a tenant exists, so the budget suits a
    /// person correcting a typo rather than a script enumerating names.
    /// </summary>
    int OrgLookupRateLimitPermitLimit { get; }

    /// <summary>Gets the sliding window over which alias lookups are counted.</summary>
    TimeSpan OrgLookupRateLimitWindow { get; }

    /// <summary>Gets the number of segments in the alias-lookup sliding window.</summary>
    int OrgLookupRateLimitSegments { get; }

    // ── Global rate limiting ─────────────────────────────────────────────────
    /// <summary>
    /// Gets whether the catch-all global rate limiter is enabled. When <c>false</c>, only the
    /// explicit per-endpoint policies apply.
    /// </summary>
    bool GlobalRateLimitEnabled { get; }

    /// <summary>Gets the request allowance per authenticated user (JWT <c>sub</c>) per <see cref="GlobalRateLimitUserWindow"/>.</summary>
    int GlobalRateLimitUserPermitLimit { get; }

    /// <summary>Gets the sliding window over which <see cref="GlobalRateLimitUserPermitLimit"/> is counted.</summary>
    TimeSpan GlobalRateLimitUserWindow { get; }

    /// <summary>Gets the number of segments the per-user sliding window is divided into.</summary>
    int GlobalRateLimitUserSegments { get; }

    /// <summary>Gets the request allowance per client IP (anonymous/external callers) per <see cref="GlobalRateLimitIpWindow"/>.</summary>
    int GlobalRateLimitIpPermitLimit { get; }

    /// <summary>Gets the sliding window over which <see cref="GlobalRateLimitIpPermitLimit"/> is counted.</summary>
    TimeSpan GlobalRateLimitIpWindow { get; }

    /// <summary>Gets the number of segments the per-IP sliding window is divided into.</summary>
    int GlobalRateLimitIpSegments { get; }

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

    // ── MinIO object storage ─────────────────────────────────────────────────
    /// <summary>Gets the MinIO / S3-compatible endpoint URL (e.g. "http://localhost:9100").</summary>
    string MinioEndpoint { get; }

    /// <summary>Gets the MinIO access key (root user).</summary>
    string MinioAccessKey { get; }

    /// <summary>Gets the MinIO secret key (root password).</summary>
    string MinioSecretKey { get; }

    /// <summary>Gets the bucket name used for storing user avatar images.</summary>
    string MinioAvatarBucket { get; }

    /// <summary>Gets the bucket name used for work-item attachments and pasted inline images.</summary>
    string MinioAttachmentBucket { get; }

    /// <summary>
    /// Gets the public base URL used to build object URLs returned to the browser
    /// (e.g. "http://localhost:9100"). May differ from <see cref="MinioEndpoint"/> in Docker.
    /// </summary>
    string MinioPublicBaseUrl { get; }

    // ── HUB Chat integration ─────────────────────────────────────────────────
    /// <summary>
    /// Gets the base URL of the HUB Chat internal API (e.g. <c>http://localhost:5001</c>).
    /// Empty string means HUB integration is disabled.
    /// </summary>
    string HubChatBaseUrl { get; }

    /// <summary>Gets the shared secret sent in <c>X-Internal-Token</c> when calling HUB Chat's /internal/* endpoints.</summary>
    string HubChatInternalToken { get; }

    /// <summary>
    /// Gets the HUB frontend base URL used to build "Open in HUB" deep-links
    /// (e.g. <c>http://localhost:4202</c>). Empty string means HUB integration is disabled.
    /// </summary>
    string HubFrontendBaseUrl { get; }

    // ── NMate (AI support assistant) ─────────────────────────────────────────
    /// <summary>
    /// Gets the base URL of the NMate internal API (e.g. <c>http://localhost:5160</c>).
    /// Empty string means NMate is disabled and <c>/api/v1/nmate/*</c> returns 404.
    /// </summary>
    string NMateBaseUrl { get; }

    /// <summary>Gets the shared secret sent in <c>X-Internal-Token</c> when calling NMate's /internal/* endpoints.</summary>
    string NMateInternalToken { get; }

    /// <summary>Gets the upper bound for one NMate call, including a whole streamed answer.</summary>
    TimeSpan NMateTimeout { get; }

    /// <summary>Gets how many questions one user may ask NMate per <see cref="NMateChatRateLimitWindow"/>.</summary>
    int NMateChatRateLimitPermitLimit { get; }

    /// <summary>Gets the sliding window for <see cref="NMateChatRateLimitPermitLimit"/>.</summary>
    TimeSpan NMateChatRateLimitWindow { get; }
}
