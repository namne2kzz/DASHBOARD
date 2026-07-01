using DASHBOARD.Application.Wiki.DTOs;
using MediatR;

namespace DASHBOARD.Application.Wiki.Queries.ListWikiPages;

/// <summary>Returns the wiki page tree for a repository. Root pages contain their children recursively.</summary>
/// <param name="RepositoryId">The repository to query.</param>
public sealed record ListWikiPagesQuery(Guid RepositoryId) : IRequest<IReadOnlyList<WikiPageDto>>;
