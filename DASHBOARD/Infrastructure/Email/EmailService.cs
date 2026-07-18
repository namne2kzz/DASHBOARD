using System.Reflection;
using DASHBOARD.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DASHBOARD.Infrastructure.Email;

/// <summary>Sends transactional emails via SMTP using MailKit. HTML bodies are loaded from embedded template files.</summary>
internal sealed class EmailService(IAppSettings settings) : IEmailService
{
    private static readonly Assembly Assembly = typeof(EmailService).Assembly;

    /// <summary>Sends a repository invitation email using the <c>InvitationEmail.html</c> embedded template.</summary>
    /// <param name="toEmail">Recipient email address; also shown in the email body as the required Google account.</param>
    /// <param name="inviteLink">Accept URL with the token in the URL fragment.</param>
    /// <param name="invitedByName">Display name of the user who issued the invite.</param>
    /// <param name="repositoryName">Display name of the repository the invitee is being invited to.</param>
    /// <param name="expiryMinutes">Token validity window displayed in the email body.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendInvitationAsync(
        string toEmail,
        string inviteLink,
        string invitedByName,
        string repositoryName,
        int expiryMinutes,
        CancellationToken ct)
    {
        var body = await LoadTemplateAsync("InvitationEmail.html", ct);

        body = body
            .Replace("{{InvitedByName}}", invitedByName)
            .Replace("{{InvitedEmail}}", toEmail)
            .Replace("{{InviteLink}}", inviteLink)
            .Replace("{{RepositoryName}}", repositoryName)
            .Replace("{{ExpiryMinutes}}", expiryMinutes.ToString());

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(repositoryName, settings.EmailFromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = $"You've been invited to join {repositoryName}";
        message.Body    = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        // StartTlsWhenAvailable upgrades to TLS when the server offers it (e.g. Gmail on 587) and
        // falls back to plaintext when it doesn't (local dev relays like Mailpit don't terminate TLS)
        // — StartTls alone would hard-fail against a relay that never advertises STARTTLS.
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, ct);

        // Local dev relays like Mailpit advertise no SASL mechanism at all, so an unconditional
        // AuthenticateAsync throws NotSupportedException — only authenticate when the server
        // actually offers a mechanism (real providers like Gmail always do).
        if (client.AuthenticationMechanisms.Count > 0)
        {
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    /// <summary>
    /// Loads an embedded HTML template by filename, searching all embedded resources in this assembly.
    /// Resilient to folder renames and assembly name changes — only the filename must match.
    /// </summary>
    /// <param name="fileName">Template filename including extension (e.g. "InvitationEmail.html").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The template HTML string with placeholders intact.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no embedded resource matches the filename.</exception>
    private static async Task<string> LoadTemplateAsync(string fileName, CancellationToken ct)
    {
        var resourceName = Assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Email template '{fileName}' not found as an embedded resource. " +
                $"Ensure the file is marked as EmbeddedResource in the .csproj.");

        await using var stream = Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
