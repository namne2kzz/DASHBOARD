namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Abstraction for sending transactional emails. Implementation lives in Infrastructure (MailKit).</summary>
public interface IEmailService
{
    /// <summary>Sends a repository invitation email containing a one-time accept link.</summary>
    /// <param name="toEmail">Recipient email address (also shown in the email body as the required Google account).</param>
    /// <param name="inviteLink">Accept URL with the token in the fragment.</param>
    /// <param name="invitedByName">Display name of the user who issued the invite.</param>
    /// <param name="repositoryName">Display name of the repository the invitee is being invited to.</param>
    /// <param name="expiryMinutes">Token validity window displayed in the email.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendInvitationAsync(string toEmail, string inviteLink, string invitedByName, string repositoryName, int expiryMinutes, CancellationToken ct);

    /// <summary>Notifies the assignee (and optionally other watchers) that a work item's state has changed.</summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="recipientName">Recipient display name.</param>
    /// <param name="workItemNumber">Formatted work item number (e.g. "DASH-12").</param>
    /// <param name="workItemTitle">Work item title.</param>
    /// <param name="oldState">Previous state label.</param>
    /// <param name="newState">New state label.</param>
    /// <param name="changedByName">Display name of the user who made the change.</param>
    /// <param name="repositoryName">Repository display name.</param>
    /// <param name="itemUrl">Deep-link to the work item in the app.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendStateChangedAsync(string toEmail, string recipientName, string workItemNumber, string workItemTitle,
        string oldState, string newState, string changedByName, string repositoryName, string itemUrl, CancellationToken ct);

    /// <summary>Notifies a user that a work item has been assigned to or unassigned from them.</summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="recipientName">Recipient display name.</param>
    /// <param name="workItemNumber">Formatted work item number (e.g. "DASH-12").</param>
    /// <param name="workItemTitle">Work item title.</param>
    /// <param name="assigned">True when the recipient is being assigned; false when unassigned.</param>
    /// <param name="changedByName">Display name of the user who made the change.</param>
    /// <param name="repositoryName">Repository display name.</param>
    /// <param name="itemUrl">Deep-link to the work item in the app.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAssigneeChangedAsync(string toEmail, string recipientName, string workItemNumber, string workItemTitle,
        bool assigned, string changedByName, string repositoryName, string itemUrl, CancellationToken ct);
}
