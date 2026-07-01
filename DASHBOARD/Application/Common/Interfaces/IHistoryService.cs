namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Appends audit-trail entries to the EF change tracker. Entries are persisted in the same <c>SaveChangesAsync</c> call as the work-item mutation.</summary>
public interface IHistoryService
{
    /// <summary>Stages a new <see cref="DASHBOARD.Domain.Entities.HistoryEntry"/> in the change tracker.</summary>
    /// <param name="sprintTaskId">The sprint task (work item) that was changed.</param>
    /// <param name="repositoryId">The owning repository (denormalized).</param>
    /// <param name="authorId">The user who performed the change.</param>
    /// <param name="message">Human-readable description of the change.</param>
    void Record(Guid sprintTaskId, Guid repositoryId, Guid authorId, string message);
}
