using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogTitle;

/// <summary>Renames a backlog item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to rename.</param>
/// <param name="Title">New title (1–500 chars).</param>
public sealed record UpdateBacklogTitleCommand(
    Guid   RepositoryId,
    Guid   ItemId,
    string Title) : IRequest<Result>;
