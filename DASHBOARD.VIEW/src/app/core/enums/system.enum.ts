/** Matches C# WorkItemType enum — numeric payload from the API. */
export enum WorkItemApiType {
  UserStory   = 0,
  Bug         = 1,
  Task        = 2,
  Improvement = 3,
  TestPlan    = 4,
}


/** Matches C# WorkItemState enum — numeric payload from the API. */
export enum WorkItemApiState {
  New           = 0,
  Active        = 1,
  Closed        = 2,
  Completed     = 3,
  Resolved      = 4,
  Reopen        = 5,
  InTest        = 6,
  DoneTest      = 7,
  DevCompleted  = 8,
  TestCompleted = 9,
  Design        = 10,
  Review        = 11,
  Run           = 12,
  Pass          = 13,
  Fail          = 14,
}

// ── Backlog ───────────────────────────────────────────────────────────────────

/** Matches C# BacklogItemType enum — numeric payload from the API. */
export enum BacklogItemApiType {
  Epic      = 0,
  Feature   = 1,
  UserStory = 2,
}

/** Matches C# BacklogItemState enum — numeric payload from the API. */
export enum BacklogItemApiState {
  New       = 0,
  Refining  = 1,
  Ready     = 2,
  Committed = 3,
}

/** Matches C# TshirtSize enum — numeric payload from the API. */
export enum BacklogTshirtSize {
  XS = 0,
  S  = 1,
  M  = 2,
  L  = 3,
  XL = 4,
}

// ── Sprint ────────────────────────────────────────────────────────────────────

/** Matches C# SprintTaskType enum — numeric payload from the API. */
export enum SprintTaskApiType  { UserStory = 0, Task = 1, Bug = 2, TestPlan = 3 }

/** Matches C# WorkItemState enum — numeric payload from the API. */
export enum SprintTaskApiState {
  Open       = 0,
  ToDo       = 1,
  InProgress = 2,
  InReview   = 3,
  Verified   = 4,  // Bug only
  Running    = 5,  // TestPlan only
  Done       = 6,
  Passed     = 7,  // TestPlan only
  Failed     = 8,  // TestPlan only
  Closed     = 9,
}

/** Macro category — maps multiple states to ToDo/InProgress/Done for system logic. */
export enum StateCategory { ToDo = 'todo', InProgress = 'in-progress', Done = 'done' }

/** Returns the StateCategory for a given state. @param state The API state value. @returns The macro category for system logic. */
export function getStateCategory(state: SprintTaskApiState): StateCategory {
  switch (state) {
    case SprintTaskApiState.Open:
    case SprintTaskApiState.ToDo:
      return StateCategory.ToDo;
    case SprintTaskApiState.InProgress:
    case SprintTaskApiState.InReview:
    case SprintTaskApiState.Verified:
    case SprintTaskApiState.Running:
      return StateCategory.InProgress;
    case SprintTaskApiState.Done:
    case SprintTaskApiState.Passed:
    case SprintTaskApiState.Failed:
    case SprintTaskApiState.Closed:
      return StateCategory.Done;
  }
}

/** Returns allowed states for a given work item type. @param type The work item type. @returns Array of allowed SprintTaskApiState values for that type. */
export function getAllowedStates(type: SprintTaskApiType): SprintTaskApiState[] {
  switch (type) {
    case SprintTaskApiType.UserStory:
      return [SprintTaskApiState.Open, SprintTaskApiState.InProgress, SprintTaskApiState.Done, SprintTaskApiState.Closed];
    case SprintTaskApiType.Task:
      return [SprintTaskApiState.ToDo, SprintTaskApiState.InProgress, SprintTaskApiState.InReview, SprintTaskApiState.Done, SprintTaskApiState.Closed];
    case SprintTaskApiType.Bug:
      return [SprintTaskApiState.Open, SprintTaskApiState.InProgress, SprintTaskApiState.InReview, SprintTaskApiState.Verified, SprintTaskApiState.Done, SprintTaskApiState.Closed];
    case SprintTaskApiType.TestPlan:
      return [SprintTaskApiState.Open, SprintTaskApiState.Running, SprintTaskApiState.Passed, SprintTaskApiState.Failed, SprintTaskApiState.Closed];
  }
}

/** Matches C# WorkItemPriority enum — numeric payload from the API. */
export enum WorkItemApiPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

// ── Permissions ───────────────────────────────────────────────────────────────

/**
 * Granular permissions assignable to custom roles.
 * Matches C# SystemFunction enum — numeric payload from the API.
 */
export enum Permission {
  // Repository
  ViewRepository  = 0,
  EditRepository  = 1,
  // Members & Access
  ManageMembers   = 2,
  ManageRoles     = 3,
  InviteMembers   = 4,
  ManageMetadata  = 5,
  // Work Items (Sprint Tasks)
  CreateWorkItem  = 6,
  EditWorkItem    = 7,
  DeleteWorkItem  = 8,
  AssignWorkItem  = 9,
  // Backlog
  ManageBacklog   = 10,
  PromoteToSprint = 11,
  // Sprint
  ManageSprint    = 12,
  ActivateSprint  = 13,
  // Capacity / Board
  ManageCapacity  = 14,
  ManageBoard     = 16,
  // Analytics & Integrations
  ViewAnalytics   = 17,
  ManagePipeline  = 18,
  ManageRepo      = 19,
  // Collaboration (HUB)
  ManageChannels  = 20,
}
