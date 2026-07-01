using DASHBOARD.Application.Members.DTOs;
using MediatR;

namespace DASHBOARD.Application.Members.Queries.ListMembers;

/// <summary>Returns all members of a repository with their role and user details.</summary>
/// <param name="RepositoryId">The repository to query.</param>
/// <param name="RoleFilter">Optional filter by team-role (discipline) value.</param>
public sealed record ListMembersQuery(
    Guid    RepositoryId,
    string? RoleFilter = null) : IRequest<IReadOnlyList<MemberDto>>;
