using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;

/// <summary>Updates the editable fields of a work item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="TaskId">The work item to update.</param>
/// <param name="Title">New title.</param>
/// <param name="Description">New markdown description.</param>
/// <param name="Priority">New priority.</param>
/// <param name="AssignedToId">New assignee; null to unassign. Must be null for UserStory.</param>
/// <param name="StoryPoints">Story points (UserStory only).</param>
/// <param name="OriginalEstimate">Original estimate in hours (Task/Bug only).</param>
/// <param name="StepsToReproduce">Bug reproduction steps (Bug only).</param>
/// <param name="Environment">Bug environment (Bug only).</param>
/// <param name="RootCause">Root cause (Bug only).</param>
/// <param name="Solution">Solution description (Bug only).</param>
/// <param name="Impaction">Impact description (Bug only).</param>
/// <param name="UnitTest">Unit test notes (Task/Bug only).</param>
/// <param name="DesignReview">Design review notes (Task/Bug only).</param>
/// <param name="TestSteps">Test steps (TestPlan only).</param>
/// <param name="Automated">Whether automated (TestPlan only).</param>
public sealed record UpdateSprintTaskCommand(
    Guid             RepositoryId,
    Guid             TaskId,
    string           Title,
    string           Description,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    int              StoryPoints,
    decimal          OriginalEstimate,
    string?          StepsToReproduce,
    string?          Environment,
    string?          RootCause,
    string?          Solution,
    string?          Impaction,
    string?          UnitTest,
    string?          DesignReview,
    List<string>?    TestSteps,
    bool?            Automated) : IRequest<Result>;
