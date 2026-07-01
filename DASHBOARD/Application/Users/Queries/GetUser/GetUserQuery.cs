using DASHBOARD.Application.Users.DTOs;
using MediatR;

namespace DASHBOARD.Application.Users.Queries.GetUser;

/// <summary>Returns the public profile of a single user. Any authenticated user may query any profile.</summary>
/// <param name="UserId">The ID of the user to retrieve.</param>
public sealed record GetUserQuery(Guid UserId) : IRequest<UserProfileDto>;
