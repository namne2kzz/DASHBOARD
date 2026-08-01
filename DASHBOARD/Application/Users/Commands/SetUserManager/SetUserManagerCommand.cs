using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.SetUserManager;

/// <summary>Sets (or clears) a user's manager in the organisation hierarchy. Global admins only.</summary>
/// <param name="UserId">The user whose manager is being set.</param>
/// <param name="ManagerId">The new manager's ID, or null to make the user a root (no manager).</param>
public sealed record SetUserManagerCommand(Guid UserId, Guid? ManagerId) : IRequest<Result>;
