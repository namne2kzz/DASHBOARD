using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Invitations.DTOs;

/// <summary>Read model for a single row in the repository's invitation list.</summary>
/// <param name="Id">Invitation identifier.</param>
/// <param name="Email">Email address the invite was sent to.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="DefaultRole">Team role (discipline) the invitee will be assigned on acceptance.</param>
/// <param name="RoleName">Name of the role (default or custom) granting permissions on acceptance, or null if the role was deleted since.</param>
/// <param name="InvitedByName">Display name of the user who issued the invite.</param>
/// <param name="CreatedAt">UTC timestamp the invite was sent.</param>
/// <param name="ExpiresAt">UTC timestamp the invite token expires.</param>
/// <param name="AcceptedAt">UTC timestamp the invite was accepted, or null if not yet accepted.</param>
public sealed record InvitationListItemDto(
    Guid Id,
    string Email,
    InvitationStatus Status,
    string DefaultRole,
    string? RoleName,
    string InvitedByName,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt);
