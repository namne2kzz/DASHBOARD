using DASHBOARD.Application.Wiki.DTOs;
using MediatR;

namespace DASHBOARD.Application.Wiki.Queries.GetWikiPage;

/// <summary>Returns a single wiki page with its immediate children.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="PageId">The page to fetch.</param>
public sealed record GetWikiPageQuery(Guid RepositoryId, Guid PageId) : IRequest<WikiPageDto>;
