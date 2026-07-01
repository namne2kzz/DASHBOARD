using DASHBOARD.Application.Auth.DTOs;
using MediatR;

namespace DASHBOARD.Application.Auth.Queries.GetCurrentUser;

/// <summary>Returns the full profile of the currently authenticated user from the database.</summary>
public record GetCurrentUserQuery : IRequest<UserDto>;
