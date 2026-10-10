using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Notifications.Commands.MarkRead;

/// <summary>Handles <see cref="MarkReadCommand"/>: stamps <c>IsRead = true</c> on one or all notifications.</summary>
public sealed class MarkReadCommandHandler(
    IApplicationDbContext db,
    ICurrentUserService   currentUser,
    IUnitOfWork           uow) : IRequestHandler<MarkReadCommand, Result>
{
    /// <summary>Marks one specific notification (when <see cref="MarkReadCommand.NotificationId"/> is set) or all unread notifications as read.</summary>
    /// <param name="command">The mark-read command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(MarkReadCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        if (command.NotificationId.HasValue)
        {
            var notification = await db.Set<Notification>()
                .AsTracking()
                .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.UserId == userId, ct)
                ?? throw new NotFoundException(nameof(Notification), command.NotificationId.Value);

            notification.MarkRead();
        }
        else
        {
            // Bulk-mark all unread — use ExecuteUpdate for a single UPDATE statement.
            await db.Set<Notification>()
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

            return Result.Ok;
        }

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
