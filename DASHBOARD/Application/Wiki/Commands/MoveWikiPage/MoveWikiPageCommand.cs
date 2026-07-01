using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Wiki.Commands.MoveWikiPage;

/// <summary>Changes the parent of a wiki page (moves it within the hierarchy). Prevents circular references.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="PageId">The page to move.</param>
/// <param name="NewParentId">The new parent page ID, or <c>null</c> to make it a root page.</param>
public sealed record MoveWikiPageCommand(
    Guid  RepositoryId,
    Guid  PageId,
    Guid? NewParentId) : IRequest<Result>;
