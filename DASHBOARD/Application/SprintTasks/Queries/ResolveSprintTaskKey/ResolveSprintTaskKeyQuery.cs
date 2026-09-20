using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.ResolveSprintTaskKey;

/// <summary>Resolves a formatted work-item key (e.g. "DASH-10") to its task UUID.</summary>
/// <param name="RepositoryId">The repository the task belongs to.</param>
/// <param name="Key">The formatted key, e.g. "DASH-10".</param>
public sealed record ResolveSprintTaskKeyQuery(Guid RepositoryId, string Key) : IRequest<Guid>;
