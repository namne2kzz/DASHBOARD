using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>Stages <see cref="HistoryEntry"/> records in the EF change tracker to be saved alongside the triggering work-item mutation.</summary>
public sealed class HistoryService(IApplicationDbContext db) : IHistoryService
{
    /// <inheritdoc/>
    public void Record(Guid sprintTaskId, Guid repositoryId, Guid authorId, string message) =>
        db.Set<HistoryEntry>().Add(new HistoryEntry
        {
            SprintTaskId = sprintTaskId,
            RepositoryId = repositoryId,
            AuthorId     = authorId,
            Message      = message,
        });
}
