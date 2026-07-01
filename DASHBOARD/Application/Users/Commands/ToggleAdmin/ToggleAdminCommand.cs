using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.ToggleAdmin
{
    /// <summary>
    /// Toggle the target user to global admin role
    /// </summary>
    /// <param name="TargetUserId"></param>
    public sealed record ToggleAdminCommand
    (
        Guid TargetUserId
    ) : IRequest<Result>;
}
