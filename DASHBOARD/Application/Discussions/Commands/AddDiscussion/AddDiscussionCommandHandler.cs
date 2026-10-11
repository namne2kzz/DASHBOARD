using System.Text.RegularExpressions;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Commands.AddDiscussion;

/// <summary>Handles <see cref="AddDiscussionCommand"/>: verifies membership and sprint task existence, creates the comment, and notifies any @mentioned users.</summary>
public sealed partial class AddDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    INotificationService  notifications) : IRequestHandler<AddDiscussionCommand, DiscussionDto>
{
    /// <summary>Matches a stored mention token <c>@[Display Name](user-guid)</c> and captures the user id.</summary>
    [GeneratedRegex(@"@\[[^\]]+\]\((?<id>[0-9a-fA-F-]{36})\)", RegexOptions.Compiled)]
    private static partial Regex MentionRegex();

    /// <summary>Validates membership, creates the discussion entry, notifies mentioned users, and returns the DTO.</summary>
    /// <param name="command">The add command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="DiscussionDto"/>.</returns>
    public async Task<DiscussionDto> Handle(AddDiscussionCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var task = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.Id == command.SprintTaskId && t.RepositoryId == command.RepositoryId)
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.SprintTaskId);

        var author = await db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.UserId, ct)!;

        var entry = new DiscussionEntry
        {
            SprintTaskId = command.SprintTaskId,
            RepositoryId = command.RepositoryId,
            AuthorId     = user.UserId,
            Body         = command.Body,
        };
        db.Set<DiscussionEntry>().Add(entry);
        await uow.CommitAsync(ct);

        await NotifyMentionedUsersAsync(command.Body, author!, task.Title, command.RepositoryId, command.SprintTaskId, ct);

        return new DiscussionDto(entry.Id, entry.SprintTaskId, user.UserId,
            author!.Name, author.AvatarClass, entry.Body, entry.CreatedAt, null);
    }

    /// <summary>Parses @mention tokens from the body and pushes a notification to each distinct mentioned user (except the author) who has mention notifications enabled.</summary>
    private async Task NotifyMentionedUsersAsync(
        string body, User author, string taskTitle,
        Guid repositoryId, Guid sprintTaskId, CancellationToken ct)
    {
        var mentionedIds = MentionRegex().Matches(body)
            .Select(m => Guid.TryParse(m.Groups["id"].Value, out var id) ? id : (Guid?)null)
            .Where(id => id is { } g && g != author.Id)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (mentionedIds.Count == 0)
            return;

        // Only notify users who are members of the repository (a mention token could be stale or forged).
        var validRecipients = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => m.RepositoryId == repositoryId && mentionedIds.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (validRecipients.Count == 0)
            return;

        var prefs = await NotifyPrefsHelper.MentionBatchAsync(db, validRecipients, ct);
        var url   = $"/{repositoryId}/items/{sprintTaskId}";

        foreach (var userId in validRecipients)
        {
            if (!prefs.TryGetValue(userId, out var enabled) || !enabled)
                continue;

            await notifications.NotifyAsync(
                userId,
                NotificationType.MentionedInDiscussion,
                $"{author.Name} mentioned you",
                $"in \"{taskTitle}\"",
                url, ct);
        }
    }
}
