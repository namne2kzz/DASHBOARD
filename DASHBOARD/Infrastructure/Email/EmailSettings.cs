namespace DASHBOARD.Infrastructure.Email;

/// <summary>SMTP configuration bound from <c>appsettings.json → EmailSettings</c>.</summary>
public sealed class EmailSettings
{
    /// <summary>Gets or sets the SMTP server hostname (e.g. "smtp.gmail.com").</summary>
    public string SmtpHost { get; set; } = default!;

    /// <summary>Gets or sets the SMTP server port (587 for STARTTLS, 465 for SSL).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Gets or sets the SMTP login username.</summary>
    public string Username { get; set; } = default!;

    /// <summary>Gets or sets the SMTP login password or app-specific password.</summary>
    public string Password { get; set; } = default!;

    /// <summary>Gets or sets the display name shown in the From field.</summary>
    public string FromName { get; set; } = "Dashboard";

    /// <summary>Gets or sets the From email address.</summary>
    public string FromEmail { get; set; } = default!;
}
