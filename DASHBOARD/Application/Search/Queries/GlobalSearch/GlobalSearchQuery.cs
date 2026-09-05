using DASHBOARD.Application.Search.DTOs;
using MediatR;

namespace DASHBOARD.Application.Search.Queries.GlobalSearch;

/// <summary>Searches Backlog items and Sprint tasks within a repository by keyword.</summary>
/// <param name="RepositoryId">The repository to search within.</param>
/// <param name="Term">The keyword to match against titles and content.</param>
public sealed record GlobalSearchQuery(Guid RepositoryId, string Term) : IRequest<IReadOnlyList<SearchResultItemDto>>;
