using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Attachments.Queries.ListAttachments;

/// <summary>Handles <see cref="ListAttachmentsQuery"/>: checks membership then returns the work item's attachments.</summary>
public sealed class ListAttachmentsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    /// <summary>Validates membership and returns the attachments ordered newest first.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="AttachmentDto"/>.</returns>
    public async Task<IReadOnlyList<AttachmentDto>> Handle(ListAttachmentsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        return await db.Set<WorkItemAttachment>()
            .AsNoTracking()
            .Where(a => a.SprintTaskId == query.SprintTaskId && a.RepositoryId == query.RepositoryId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(
                a.Id, a.FileName, a.ContentType, a.SizeBytes, a.PublicUrl,
                a.UploadedBy!.Name, a.UploadedBy.AvatarClass, a.CreatedAt))
            .ToListAsync(ct);
    }
}
