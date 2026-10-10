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

        await SendAsync(toEmail, toEmail, $"You've been invited to join {repositoryName}", body, ct);
    }

    /// <summary>Constructs and sends an HTML email over SMTP.</summary>
    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("NFlow", settings.EmailFromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body    = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        // StartTlsWhenAvailable upgrades to TLS when the server offers it (e.g. Gmail on 587) and
        // falls back to plaintext when it doesn't (local dev relays like Mailpit don't terminate TLS).
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, ct);

        // Local dev relays like Mailpit advertise no SASL mechanism; only authenticate when offered.
        if (client.AuthenticationMechanisms.Count > 0)
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    /// <summary>Sends a state-change notification to the work item's assignee.</summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="recipientName">Recipient display name.</param>
    /// <param name="workItemNumber">Formatted work item number.</param>
    /// <param name="workItemTitle">Work item title.</param>
    /// <param name="oldState">Previous state label.</param>
    /// <param name="newState">New state label.</param>
    /// <param name="changedByName">Display name of who made the change.</param>
    /// <param name="repositoryName">Repository display name.</param>
    /// <param name="itemUrl">Deep-link URL to the work item.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendStateChangedAsync(
        string toEmail, string recipientName, string workItemNumber, string workItemTitle,
        string oldState, string newState, string changedByName, string repositoryName,
        string itemUrl, CancellationToken ct)
    {
        var body = await LoadTemplateAsync("StateChangedEmail.html", ct);
        body = body
            .Replace("{{RecipientName}}",  recipientName)
            .Replace("{{WorkItemNumber}}", workItemNumber)
            .Replace("{{WorkItemTitle}}",  workItemTitle)
            .Replace("{{OldState}}",       oldState)
            .Replace("{{NewState}}",       newState)
            .Replace("{{ChangedByName}}", changedByName)
            .Replace("{{RepositoryName}}", repositoryName)
            .Replace("{{ItemUrl}}",        itemUrl);

        await SendAsync(toEmail, recipientName, $"[{workItemNumber}] State changed: {oldState} → {newState}", body, ct);
    }

    /// <summary>Notifies a user they have been assigned to or unassigned from a work item.</summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="recipientName">Recipient display name.</param>
    /// <param name="workItemNumber">Formatted work item number.</param>
    /// <param name="workItemTitle">Work item title.</param>
    /// <param name="assigned">True = assigned; false = unassigned.</param>
    /// <param name="changedByName">Display name of who made the change.</param>
    /// <param name="repositoryName">Repository display name.</param>
    /// <param name="itemUrl">Deep-link URL to the work item.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendAssigneeChangedAsync(
        string toEmail, string recipientName, string workItemNumber, string workItemTitle,
        bool assigned, string changedByName, string repositoryName, string itemUrl, CancellationToken ct)
    {
        var verb        = assigned ? "Assigned"   : "Unassigned";
        var preposition = assigned ? "to"         : "from";
        var badgeClass  = assigned ? "badge--assigned" : "badge--unassigned";

        var body = await LoadTemplateAsync("AssigneeChangedEmail.html", ct);
        body = body
            .Replace("{{RecipientName}}",      recipientName)
            .Replace("{{WorkItemNumber}}",     workItemNumber)
            .Replace("{{WorkItemTitle}}",      workItemTitle)
            .Replace("{{AssignmentVerb}}",     verb)
            .Replace("{{AssignmentPreposition}}", preposition)
            .Replace("{{AssignmentBadgeClass}}", badgeClass)
            .Replace("{{ChangedByName}}",      changedByName)
            .Replace("{{RepositoryName}}",     repositoryName)
            .Replace("{{ItemUrl}}",            itemUrl);

        await SendAsync(toEmail, recipientName, $"[{workItemNumber}] {verb}: {workItemTitle}", body, ct);
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
