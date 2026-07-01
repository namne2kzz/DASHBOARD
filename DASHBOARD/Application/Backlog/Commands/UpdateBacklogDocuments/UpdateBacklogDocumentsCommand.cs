using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogDocuments;

/// <summary>Replaces the document list of a backlog item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to update.</param>
/// <param name="Documents">New ordered list of document titles or links.</param>
public sealed record UpdateBacklogDocumentsCommand(
    Guid                  RepositoryId,
    Guid                  ItemId,
    IReadOnlyList<string> Documents) : IRequest<Result>;
