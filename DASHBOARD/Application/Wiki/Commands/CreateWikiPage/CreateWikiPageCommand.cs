using DASHBOARD.Application.Wiki.DTOs;
using MediatR;

namespace DASHBOARD.Application.Wiki.Commands.CreateWikiPage;

/// <summary>Creates a new wiki page. Requires <see cref="DASHBOARD.Domain.Enums.SystemFunction.ManageWiki"/>.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="Title">Page title.</param>
/// <param name="Content">Page content (HTML or Markdown).</param>
/// <param name="ParentId">Parent page ID; null for root-level pages.</param>
public sealed record CreateWikiPageCommand(
    Guid    RepositoryId,
    string  Title,
    string  Content,
    Guid?   ParentId) : IRequest<WikiPageDto>;
