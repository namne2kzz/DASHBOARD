namespace DASHBOARD.Application.Contracts;

/// <summary>
/// MassTransit message published when a repository invite is created.
/// Consumed by <c>InvitationCreatedConsumer</c> in Infrastructure, which sends the SMTP email.
/// </summary>
/// <param name="ToEmail">Recipient email address.</param>
/// <param name="InviteLink">Accept URL with token in the fragment (never in query string).</param>
/// <param name="InvitedByName">Display name of the user who issued the invite.</param>
/// <param name="ExpiryMinutes">Token validity window shown in the email body.</param>
public sealed record InvitationCreatedMessage(
    string ToEmail,
    string InviteLink,
    string InvitedByName,
    int ExpiryMinutes);
