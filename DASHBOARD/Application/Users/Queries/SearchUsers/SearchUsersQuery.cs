using DASHBOARD.Application.Users.DTOs;
using MediatR;

namespace DASHBOARD.Application.Users.Queries.SearchUsers;

/// <summary>Searches active users by name/email fragment. Accessible to any authenticated user — used by the member-picker flow.</summary>
/// <param name="Term">Name or email fragment to match (minimum 2 characters).</param>
/// <param name="ExcludeRepoId">When provided, users already in this repository are excluded from results.</param>
public sealed record SearchUsersQuery(
    string Term,
    Guid?  ExcludeRepoId = null) : IRequest<IReadOnlyList<UserPickerItemDto>>;
