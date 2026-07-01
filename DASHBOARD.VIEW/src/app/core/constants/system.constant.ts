import { WorkItemApiState, Permission, SprintTaskApiState } from '../enums/system.enum';
import { WorkItemPriority, WorkItemStatus, WorkItemType } from '../../models/work-item.model';
import type { BacklogLevel, TshirtSize } from '../../models/backlog.model';

// ── Permissions ──────────────────────────────────────────────────────────────

export const PERMISSION_LABELS: Record<Permission, string> = {
  [Permission.ViewRepository]: 'View Repository',
  [Permission.EditRepository]: 'Edit Repository',
  [Permission.ManageSettings]: 'Manage Settings',
  [Permission.CreateWorkItem]: 'Create Work Item',
  [Permission.EditWorkItem]:   'Edit Work Item',
  [Permission.DeleteWorkItem]: 'Delete Work Item',
  [Permission.ManageSprint]:   'Manage Sprint',
  [Permission.ManageCapacity]: 'Manage Capacity',
  [Permission.ManageWiki]:     'Manage Wiki',
  [Permission.ManageBoard]:    'Manage Board',
};

// ── Work item status / priority ───────────────────────────────────────────────

export const TASK_STATUSES: WorkItemStatus[] = ['todo', 'in-progress', 'done'];

export const STATUS_LABELS: Record<WorkItemStatus, string> = {
  todo:          'To Do',
  'in-progress': 'In Progress',
  done:          'Done',
};

export const PRIORITY_LABELS: Record<WorkItemPriority, string> = {
  critical: 'Critical',
  high:     'High',
  medium:   'Medium',
  low:      'Low',
};

// ── Work item type ────────────────────────────────────────────────────────────

export const WORK_ITEM_TYPE_LABELS: Record<WorkItemType, string> = {
  'user-story': 'User Story',
  bug:          'Bug',
  task:         'Task',
  improvement:  'Improvement',
  'test-plan':  'Test Plan',
};

/** Accent text color used on cards / icons. */
export const WORK_ITEM_TYPE_COLOR: Record<WorkItemType, string> = {
  'user-story': 'text-sky-400',
  bug:          'text-rose-400',
  task:         'text-amber-300',
  improvement:  'text-orange-400',
  'test-plan':  'text-violet-400',
};

export const WORK_ITEM_TYPE_RING: Record<WorkItemType, string> = {
  'user-story': 'ring-sky-500/40 bg-sky-500/10',
  bug:          'ring-rose-500/40 bg-rose-500/10',
  task:         'ring-amber-400/40 bg-amber-400/10',
  improvement:  'ring-orange-400/40 bg-orange-500/10',
  'test-plan':  'ring-violet-500/40 bg-violet-500/10',
};

/** Left accent stripe on board cards (Tailwind border-l-4 + color). */
export const WORK_ITEM_TYPE_LEFT_BORDER: Record<WorkItemType, string> = {
  'user-story': 'border-l-sky-500',
  bug:          'border-l-rose-500',
  task:         'border-l-amber-400',
  improvement:  'border-l-orange-500',
  'test-plan':  'border-l-violet-500',
};

// ── Backlog estimation ────────────────────────────────────────────────────────

/** Ordered backlog hierarchy levels. */
export const BACKLOG_LEVELS: BacklogLevel[] = ['epic', 'feature', 'user-story'];

/** Allowed Fibonacci story-point values for backlog estimation. */
export const FIBONACCI_POINTS: number[] = [1, 2, 3, 5, 8, 13, 21];

/** Allowed T-shirt sizes for backlog rough estimation. */
export const TSHIRT_SIZES: TshirtSize[] = ['XS', 'S', 'M', 'L', 'XL'];

// ── Smart board ───────────────────────────────────────────────────────────────

export const SMART_BOARD_SWIMLANES = [
  { id: 'expedite' as const, name: 'Expedite', priorityValue: 'High'   as const },
  { id: 'standard' as const, name: 'Standard', priorityValue: 'Medium' as const },
];

/** Maps SmartBoardState int (0–3) to its string label. */
export const SMART_BOARD_STATES = ['New', 'Active', 'Resolved', 'Closed'] as const;

/** Maps WorkItemPriority int (0–3) to its string label. */
export const SMART_BOARD_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

// ── Sprint tasks ──────────────────────────────────────────────────────────────

/** Sprint task state options ordered by workflow progression (index = API int value). */
export const SPRINT_TASK_STATE_OPTIONS = [
  { value: 'new'       as const, label: 'New',       api: 0 },
  { value: 'backlog'   as const, label: 'Backlog',   api: 1 },
  { value: 'todo'      as const, label: 'To Do',     api: 2 },
  { value: 'active'    as const, label: 'Active',    api: 3 },
  { value: 'in-review' as const, label: 'In Review', api: 4 },
  { value: 'done'      as const, label: 'Done',      api: 5 },
] as const;

// ── Sprint task state display ─────────────────────────────────────────────────

/** Tailwind badge classes per SprintTaskApiState for kanban cards. */
export const SPRINT_TASK_STATE_BADGE: Record<SprintTaskApiState, string> = {
  [SprintTaskApiState.New]:      'bg-slate-700/80 text-slate-200 ring-slate-600',
  [SprintTaskApiState.Backlog]:  'bg-indigo-700/80 text-indigo-200 ring-indigo-600',
  [SprintTaskApiState.Todo]:     'bg-slate-600/80 text-slate-100 ring-slate-500',
  [SprintTaskApiState.Active]:   'bg-blue-600/90 text-blue-50 ring-blue-500',
  [SprintTaskApiState.InReview]: 'bg-amber-500/90 text-amber-950 ring-amber-400',
  [SprintTaskApiState.Done]:     'bg-emerald-600/90 text-emerald-50 ring-emerald-500',
};

/** Display labels per SprintTaskApiState. */
export const SPRINT_TASK_STATE_LABEL: Record<SprintTaskApiState, string> = {
  [SprintTaskApiState.New]:      'New',
  [SprintTaskApiState.Backlog]:  'Backlog',
  [SprintTaskApiState.Todo]:     'To Do',
  [SprintTaskApiState.Active]:   'Active',
  [SprintTaskApiState.InReview]: 'In Review',
  [SprintTaskApiState.Done]:     'Done',
};

// ── State ─────────────────────────────────────────────────────────────────────

export const STATE_LABELS: Record<WorkItemApiState, string> = {
  [WorkItemApiState.New]:           'New',
  [WorkItemApiState.Active]:        'Active',
  [WorkItemApiState.Closed]:        'Closed',
  [WorkItemApiState.Completed]:     'Completed',
  [WorkItemApiState.Resolved]:      'Resolved',
  [WorkItemApiState.Reopen]:        'Reopen',
  [WorkItemApiState.InTest]:        'In Test',
  [WorkItemApiState.DoneTest]:      'Done Test',
  [WorkItemApiState.DevCompleted]:  'Dev Completed',
  [WorkItemApiState.TestCompleted]: 'Test Completed',
  [WorkItemApiState.Design]:        'Design',
  [WorkItemApiState.Review]:        'Review',
  [WorkItemApiState.Run]:           'Run',
  [WorkItemApiState.Pass]:          'Pass',
  [WorkItemApiState.Fail]:          'Fail',
};

/** Tailwind classes for each state badge in the history timeline. */
export const STATE_BADGE_CLASSES: Record<WorkItemApiState, string> = {
  [WorkItemApiState.New]:           'bg-slate-700/80 text-slate-200 ring-slate-600',
  [WorkItemApiState.Active]:        'bg-blue-600/90 text-blue-50 ring-blue-500',
  [WorkItemApiState.Closed]:        'bg-emerald-700/90 text-emerald-50 ring-emerald-600',
  [WorkItemApiState.Completed]:     'bg-emerald-600/90 text-emerald-50 ring-emerald-500',
  [WorkItemApiState.Resolved]:      'bg-green-600/90 text-green-50 ring-green-500',
  [WorkItemApiState.Reopen]:        'bg-amber-500/90 text-amber-950 ring-amber-400',
  [WorkItemApiState.InTest]:        'bg-orange-600/90 text-orange-50 ring-orange-500',
  [WorkItemApiState.DoneTest]:      'bg-teal-600/90 text-teal-50 ring-teal-500',
  [WorkItemApiState.DevCompleted]:  'bg-violet-600/90 text-violet-50 ring-violet-500',
  [WorkItemApiState.TestCompleted]: 'bg-indigo-600/90 text-indigo-50 ring-indigo-500',
  [WorkItemApiState.Design]:        'bg-purple-600/90 text-purple-50 ring-purple-500',
  [WorkItemApiState.Review]:        'bg-sky-600/90 text-sky-50 ring-sky-500',
  [WorkItemApiState.Run]:           'bg-cyan-600/90 text-cyan-50 ring-cyan-500',
  [WorkItemApiState.Pass]:          'bg-green-500/90 text-green-50 ring-green-400',
  [WorkItemApiState.Fail]:          'bg-red-600/90 text-red-50 ring-red-500',
};
