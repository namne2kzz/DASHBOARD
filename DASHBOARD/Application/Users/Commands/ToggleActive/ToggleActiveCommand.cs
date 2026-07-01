using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.ToggleActive
{
    /// <summary>Activates or deactivates a system user account by toggling the soft-delete flag.</summary>
    /// <param name="TargetUserId">ID of the user to activate or deactivate.</param>
    public sealed record ToggleActiveCommand
    (
        Guid TargetUserId
    ) : IRequest<Result>;
}
