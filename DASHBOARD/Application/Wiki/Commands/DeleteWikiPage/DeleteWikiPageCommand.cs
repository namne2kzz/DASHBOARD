using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Wiki.Commands.DeleteWikiPage;

/// <summary>Soft-deletes a wiki page. Fails if the page has children.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="PageId">The page to delete.</param>
public sealed record DeleteWikiPageCommand(Guid RepositoryId, Guid PageId) : IRequest<Result>;
