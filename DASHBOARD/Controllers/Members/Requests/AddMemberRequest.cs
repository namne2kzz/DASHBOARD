using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Members.Requests;

/// <summary>HTTP request body for adding a user to a repository.</summary>
/// <param name="UserId">The user to add.</param>
/// <param name="DefaultRole">The team role (discipline).</param>
/// <param name="RoleId">Role ID (default or custom) granting permissions. Required.</param>
public sealed record AddMemberRequest(Guid UserId, string DefaultRole, Guid RoleId);
