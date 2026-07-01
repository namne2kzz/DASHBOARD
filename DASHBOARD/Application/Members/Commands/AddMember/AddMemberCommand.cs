using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Members.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Members.Commands.AddMember;

/// <summary>Adds an existing user directly to a repository with the specified role. For invite-based onboarding use the Invitations flow instead.</summary>
/// <param name="RepositoryId">The repository to add the member to.</param>
/// <param name="UserId">The user to add.</param>
/// <param name="DefaultRole">The team role (discipline).</param>
/// <param name="RoleId">The role (default or custom) granting permissions. Required.</param>
public sealed record AddMemberCommand(
    Guid      RepositoryId,
    Guid      UserId,
    string    DefaultRole,
    Guid      RoleId) : IRequest<Result<MemberDto>>;
