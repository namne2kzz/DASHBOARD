namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Abstraction for sending transactional emails. Implementation lives in Infrastructure (MailKit).</summary>
public interface IEmailService
{
    /// <summary>Sends a repository invitation email containing a one-time accept link.</summary>
    /// <param name="toEmail">Recipient email address (also shown in the email body as the required Google account).</param>
    /// <param name="inviteLink">Accept URL with the token in the fragment.</param>
    /// <param name="invitedByName">Display name of the user who issued the invite.</param>
    /// <param name="expiryMinutes">Token validity window displayed in the email.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendInvitationAsync(string toEmail, string inviteLink, string invitedByName, int expiryMinutes, CancellationToken ct);
}
