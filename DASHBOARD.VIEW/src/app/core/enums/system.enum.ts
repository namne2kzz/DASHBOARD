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

/** Matches C# SprintTaskState enum — numeric payload from the API. */
export enum SprintTaskApiState { New = 0, Backlog = 1, Todo = 2, Active = 3, InReview = 4, Done = 5 }

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
