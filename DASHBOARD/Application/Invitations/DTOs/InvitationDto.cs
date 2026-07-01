using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Invitations.DTOs;

/// <summary>Read model returned after creating a repository invitation.</summary>
/// <param name="Id">Invitation identifier.</param>
/// <param name="Email">Email address the invite was sent to.</param>
/// <param name="RepositoryId">Repository the invitee will join.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="ExpiresAt">UTC expiry timestamp of the invite token.</param>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    Guid RepositoryId,
    InvitationStatus Status,
    DateTime ExpiresAt);
