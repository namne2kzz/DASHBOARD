using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Members.Requests;

/// <summary>HTTP request body for updating a member's role assignment.</summary>
/// <param name="DefaultRole">New team role (discipline).</param>
/// <param name="RoleId">New role (default or custom) granting permissions. Required.</param>
public sealed record UpdateMemberRoleRequest(string DefaultRole, Guid RoleId);
