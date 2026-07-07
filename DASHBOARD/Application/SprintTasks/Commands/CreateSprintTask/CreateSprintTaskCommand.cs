using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;

/// <summary>Creates a new work item (sprint task, bug, or test plan).</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">Sprint to add the item to. Null for standalone Bug/TestPlan.</param>
/// <param name="ParentId">Parent task ID for sub-tasks; null for root items.</param>
/// <param name="Type">Work item type.</param>
/// <param name="Title">Item title.</param>
/// <param name="Description">Optional markdown description.</param>
/// <param name="Priority">Priority level.</param>
/// <param name="AssignedToId">Optional assignee (not applicable for UserStory).</param>
/// <param name="StoryPoints">Story point estimate (UserStory only).</param>
/// <param name="OriginalEstimate">Original time estimate in hours (Task/Bug only).</param>
/// <param name="StepsToReproduce">Steps to reproduce the issue (Bug only).</param>
/// <param name="Environment">Environment where the bug was observed (Bug only).</param>
/// <param name="RootCause">Root cause analysis (Bug only).</param>
/// <param name="Solution">Proposed or applied solution (Bug only).</param>
/// <param name="Impaction">Impact description (Bug only).</param>
/// <param name="UnitTest">Unit test notes (Task and Bug only).</param>
/// <param name="DesignReview">Design review notes (Task and Bug only).</param>
/// <param name="TestSteps">Ordered test step descriptions (TestPlan only).</param>
/// <param name="Automated">Whether the test plan is automated (TestPlan only).</param>
public sealed record CreateSprintTaskCommand(
    Guid             RepositoryId,
    Guid?            SprintId,
    Guid?            ParentId,
    SprintTaskType   Type,
    string           Title,
    string           Description,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    int              StoryPoints,
    decimal          OriginalEstimate,
    string?          StepsToReproduce = null,
    string?          Environment      = null,
    string?          RootCause        = null,
    string?          Solution         = null,
    string?          Impaction        = null,
    string?          UnitTest         = null,
    string?          DesignReview     = null,
    List<string>?    TestSteps        = null,
    bool?            Automated        = null) : IRequest<Result<SprintTaskDto>>;
