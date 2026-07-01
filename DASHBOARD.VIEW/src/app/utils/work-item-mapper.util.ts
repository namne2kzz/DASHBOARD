import { WorkItemPriority, WorkItemStatus, WorkItemType } from '../models/work-item.model';
import { WorkItemApiPriority, WorkItemApiState, WorkItemApiType } from '../core/enums/system.enum';

/** Valid states available in the State dropdown for each work item type. */
export const WORK_ITEM_TYPE_STATES: Record<WorkItemType, WorkItemApiState[]> = {
  'user-story': [
    WorkItemApiState.New,  
    WorkItemApiState.Active,
    WorkItemApiState.DevCompleted,
    WorkItemApiState.TestCompleted,
    WorkItemApiState.Closed,
  ],
  bug: [
    WorkItemApiState.New,
    WorkItemApiState.Active,
    WorkItemApiState.Resolved,
    WorkItemApiState.Reopen,
    WorkItemApiState.InTest,
    WorkItemApiState.DoneTest,
    WorkItemApiState.Closed,
  ],
  task: [
    WorkItemApiState.New,
    WorkItemApiState.Active,
    WorkItemApiState.Closed,
  ],
  improvement: [
    WorkItemApiState.New,
    WorkItemApiState.Active,
    WorkItemApiState.Completed,
  ],
  'test-plan': [
    WorkItemApiState.Design,
    WorkItemApiState.Review,
    WorkItemApiState.Run,
    WorkItemApiState.Fail,
    WorkItemApiState.Pass,
  ],
};

/** Converts the UI work-item type (kebab-case) to the API numeric enum. */
export function workItemTypeToApi(t: WorkItemType): WorkItemApiType {
  const map: Record<WorkItemType, WorkItemApiType> = {
    'user-story': WorkItemApiType.UserStory,
    bug: WorkItemApiType.Bug,
    task: WorkItemApiType.Task,
    improvement: WorkItemApiType.Improvement,
    'test-plan': WorkItemApiType.TestPlan,
  };
  return map[t];
}

/** Converts the backend API type (numeric or string) to the UI work-item type (kebab-case). */
export function workItemTypeFromApi(t: WorkItemApiType | string): WorkItemType {
  const numericMap: Record<WorkItemApiType, WorkItemType> = {
    [WorkItemApiType.UserStory]: 'user-story',
    [WorkItemApiType.Bug]: 'bug',
    [WorkItemApiType.Task]: 'task',
    [WorkItemApiType.Improvement]: 'improvement',
    [WorkItemApiType.TestPlan]: 'test-plan',
  };
  const stringMap: Record<string, WorkItemType> = {
    UserStory: 'user-story',
    Bug: 'bug',
    Task: 'task',
    Improvement: 'improvement',
    TestPlan: 'test-plan',
  };
  return typeof t === 'string' ? stringMap[t] ?? 'task' : numericMap[t];
}

/** Converts the UI priority (lowercase) to the API numeric enum. */
export function priorityToApi(p: WorkItemPriority): WorkItemApiPriority {
  const map: Record<WorkItemPriority, WorkItemApiPriority> = {
    critical: WorkItemApiPriority.Critical,
    high: WorkItemApiPriority.High,
    medium: WorkItemApiPriority.Medium,
    low: WorkItemApiPriority.Low,
  };
  return map[p];
}

/** Converts the backend API priority (numeric or string) to the UI priority (lowercase). */
export function priorityFromApi(p: WorkItemApiPriority | string): WorkItemPriority {
  const numericMap: Record<WorkItemApiPriority, WorkItemPriority> = {
    [WorkItemApiPriority.Critical]: 'critical',
    [WorkItemApiPriority.High]: 'high',
    [WorkItemApiPriority.Medium]: 'medium',
    [WorkItemApiPriority.Low]: 'low',
  };
  const stringMap: Record<string, WorkItemPriority> = {
    High: 'high',
    Medium: 'medium',
    Low: 'low',
  };
  return typeof p === 'string' ? stringMap[p] ?? 'medium' : numericMap[p];
}

/**
 * Maps a rich backend WorkItemState to the simplified 3-column board status.
 * In-progress: Active · Reopen · InTest · DoneTest · DevCompleted · TestCompleted · Run.
 * Done: Closed · Resolved · Complete · Pass · Fail.
 * Todo: New · Design · Review (default).
 */
export function stateToStatus(s: WorkItemApiState | string): WorkItemStatus {
  const state = typeof s === 'string'
    ? (WorkItemApiState[s as keyof typeof WorkItemApiState] as WorkItemApiState)
    : s;

  const inProgress = new Set<WorkItemApiState>([
    WorkItemApiState.Active,
    WorkItemApiState.Reopen,
    WorkItemApiState.InTest,
    WorkItemApiState.DoneTest,
    WorkItemApiState.DevCompleted,
    WorkItemApiState.TestCompleted,
    WorkItemApiState.Run,
    WorkItemApiState.Resolved,
    WorkItemApiState.Fail,
    WorkItemApiState.Review,
  ]);
  const done = new Set<WorkItemApiState>([
    WorkItemApiState.Closed,
    WorkItemApiState.Completed,
    WorkItemApiState.Pass,
  ]);

  if (done.has(state)) return 'done';
  if (inProgress.has(state)) return 'in-progress';
  return 'todo';
}

/**
 * Returns the API state to set when moving a work item to a board column.
 * Respects per-type state policies enforced by the backend:
 *   - UserStory  → in-progress = DevCompleted
 *   - TestPlan   → todo = Design · in-progress = Run · done = Pass
 *   - All others → todo = New · in-progress = Active · done = Closed
 */
export function statusToState(status: WorkItemStatus, type: WorkItemApiType): WorkItemApiState {
  switch (status) {
    case 'todo':
      return type === WorkItemApiType.TestPlan ? WorkItemApiState.Design : WorkItemApiState.New;
    case 'in-progress':
      if (type === WorkItemApiType.UserStory) return WorkItemApiState.DevCompleted;
      if (type === WorkItemApiType.TestPlan) return WorkItemApiState.Run;
      return WorkItemApiState.Active;
    case 'done':
      return type === WorkItemApiType.TestPlan ? WorkItemApiState.Pass : WorkItemApiState.Closed;
  }
}

// ── DnD state resolution maps ────────────────────────────────────────────────
// Each map defines which API state to set per work-item type when an item is
// dropped into a new board column via drag-and-drop.

/** State to set when an item lands in the "todo" column (any direction). */
const TODO_STATE: Record<WorkItemType, WorkItemApiState> = {
  'user-story': WorkItemApiState.New,
  bug:          WorkItemApiState.New,
  task:         WorkItemApiState.New,
  improvement:  WorkItemApiState.New,
  'test-plan':  WorkItemApiState.Design,
};

/** State to set when moving FORWARD (todo → in-progress). */
const FORWARD_IN_PROGRESS_STATE: Record<WorkItemType, WorkItemApiState> = {
  'user-story': WorkItemApiState.Active,
  bug:          WorkItemApiState.Active,
  task:         WorkItemApiState.Active,
  improvement:  WorkItemApiState.Active,
  'test-plan':  WorkItemApiState.Review,
};

/** State to set when moving FORWARD (in-progress → done). */
const FORWARD_DONE_STATE: Record<WorkItemType, WorkItemApiState> = {
  'user-story': WorkItemApiState.Closed,
  bug:          WorkItemApiState.Closed,
  task:         WorkItemApiState.Closed,
  improvement:  WorkItemApiState.Completed,
  'test-plan':  WorkItemApiState.Pass,
};

/** State to set when moving BACKWARD (done → in-progress). */
const BACKWARD_IN_PROGRESS_STATE: Record<WorkItemType, WorkItemApiState> = {
  'user-story': WorkItemApiState.Active,
  bug:          WorkItemApiState.Reopen,
  task:         WorkItemApiState.Active,
  improvement:  WorkItemApiState.Active,
  'test-plan':  WorkItemApiState.Review,
};

/**
 * Resolves the API state for an item after a DnD column change.
 * Forward (left → right): picks the first representative state for that column and type.
 * Backward (right → left): picks the appropriate "revert" state for that column and type.
 * @param currentStatus The column the item was dragged FROM.
 * @param targetStatus The column the item was dropped INTO.
 * @param workItemType The work item type.
 */
export function resolveNewState(
  currentStatus: WorkItemStatus,
  targetStatus: WorkItemStatus,
  workItemType: WorkItemType,
): WorkItemApiState {
  if (targetStatus === 'todo') return TODO_STATE[workItemType];
  if (targetStatus === 'in-progress') {
    return currentStatus === 'done'
      ? BACKWARD_IN_PROGRESS_STATE[workItemType]
      : FORWARD_IN_PROGRESS_STATE[workItemType];
  }
  return FORWARD_DONE_STATE[workItemType];
}
