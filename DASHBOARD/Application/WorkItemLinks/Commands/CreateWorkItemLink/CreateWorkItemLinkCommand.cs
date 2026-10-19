using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.WorkItemLinks.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.WorkItemLinks.Commands.CreateWorkItemLink;

/// <summary>Creates a typed link from one work item to another.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SourceTaskId">The work item the link is created on.</param>
/// <param name="TargetTaskId">The work item being linked to.</param>
/// <param name="LinkType">The relationship type.</param>
/// <param name="Description">Optional note.</param>
public sealed record CreateWorkItemLinkCommand(
    Guid             RepositoryId,
    Guid             SourceTaskId,
    Guid             TargetTaskId,
    WorkItemLinkType LinkType,
    string?          Description) : IRequest<Result<WorkItemLinkDto>>;
