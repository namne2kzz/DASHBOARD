using DASHBOARD.Application.WorkItemLinks.DTOs;
using MediatR;

namespace DASHBOARD.Application.WorkItemLinks.Queries.ListWorkItemLinks;

/// <summary>Lists all links involving a work item, from that item's perspective.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item whose links to list.</param>
public sealed record ListWorkItemLinksQuery(
    Guid RepositoryId,
    Guid SprintTaskId) : IRequest<IReadOnlyList<WorkItemLinkDto>>;
