using DASHBOARD.Application.Users.DTOs;
using MediatR;

namespace DASHBOARD.Application.Users.Queries.GetUserHierarchy;

/// <summary>Returns the organisation-chart slice centred on a user (managers above, peers, and reports below).</summary>
/// <param name="UserId">The focus user.</param>
public sealed record GetUserHierarchyQuery(Guid UserId) : IRequest<UserHierarchyDto>;
