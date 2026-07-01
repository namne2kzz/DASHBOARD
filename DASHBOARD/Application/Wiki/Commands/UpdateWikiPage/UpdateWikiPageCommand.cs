using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Wiki.Commands.UpdateWikiPage;

/// <summary>Updates the title and content of a wiki page.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="PageId">The page to update.</param>
/// <param name="Title">New title.</param>
/// <param name="Content">New content.</param>
public sealed record UpdateWikiPageCommand(
    Guid   RepositoryId,
    Guid   PageId,
    string Title,
    string Content) : IRequest<Result>;
