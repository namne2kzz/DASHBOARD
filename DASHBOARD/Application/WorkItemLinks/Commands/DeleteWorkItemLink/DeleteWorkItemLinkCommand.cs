using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.WorkItemLinks.Commands.DeleteWorkItemLink;

/// <summary>Removes a work-item link.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="LinkId">The link to delete.</param>
public sealed record DeleteWorkItemLinkCommand(
    Guid RepositoryId,
    Guid LinkId) : IRequest<Result>;
