using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Roles.Requests;

/// <summary>HTTP request body for creating or updating a custom role.</summary>
/// <param name="Name">Role display name.</param>
/// <param name="Description">Optional purpose description.</param>
/// <param name="Permissions">List of permitted system functions.</param>
public sealed record CreateRoleRequest(
    string               Name,
    string?              Description,
    List<SystemFunction> Permissions);
