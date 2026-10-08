import { WorkItemApiState, Permission, SprintTaskApiState } from '../enums/system.enum';
import { WorkItemPriority, WorkItemStatus, WorkItemType } from '../../models/work-item.model';
import type { BacklogLevel, TshirtSize } from '../../models/backlog.model';
import type { DateFormat, TimezoneId } from '../../models/preferences.model';

// ── Preferences ───────────────────────────────────────────────────────────────

/** Maps a {@link DateFormat} to the Angular date-part pattern used by DateTimeService. */
export const DATE_FORMAT_PATTERN: Record<DateFormat, string> = {
  dmy: 'dd/MM/y',
  mdy: 'MM/dd/y',
  ymd: 'y-MM-dd',
};

/** Maps a {@link TimezoneId} to an Angular `formatDate` timezone offset (undefined = browser local). */
export const TIMEZONE_OFFSET: Record<TimezoneId, string | undefined> = {
  '':      undefined,
  utc7:    '+0700',
  utc0:    '+0000',
  'utc-5': '-0500',
  utc9:    '+0900',
};

/**
 * Server-side UserSetting keys — must match {@link UserSettingKeys} constants in the backend.
 * Used by PreferencesService to read/write settings via the API.
 */
export const SETTING_KEYS = {
  dateFormat:   'ui.date-format',
  timezone:     'ui.timezone',
  language:     'ui.language',
  notifyEmail:  'notify.email',
  notifyPush:   'notify.push',
  notifyDigest: 'notify.digest',
  mentionsOnly: 'notify.mentions-only',
  reduceMotion: 'a11y.reduce-motion',
  compactMode:  'ui.compact-mode',
  avatarUrl:    'profile.avatar-url',
} as const;

// ── Avatar ───────────────────────────────────────────────────────────────────

/** Maximum file size allowed for avatar upload. */
export const AVATAR_MAX_BYTES = 5 * 1024 * 1024;

/** Accepted MIME types for avatar image upload. */
export const AVATAR_ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'] as const;

/** Available avatar background colour classes (Tailwind). */
export const AVATAR_COLORS = [
  'bg-sky-600', 'bg-blue-600', 'bg-violet-600', 'bg-purple-600',
  'bg-pink-600', 'bg-rose-600', 'bg-red-600', 'bg-orange-600',
  'bg-amber-500', 'bg-yellow-500', 'bg-lime-600', 'bg-green-600',
  'bg-emerald-600', 'bg-teal-600', 'bg-cyan-600', 'bg-slate-600',
] as const;

// ── Permissions ──────────────────────────────────────────────────────────────

export const PERMISSION_LABELS: Record<Permission, string> = {
  // Repository
  [Permission.ViewRepository]:  'View Repository',
  [Permission.EditRepository]:  'Edit Repository',
  // Members & Access
  [Permission.ManageMembers]:   'Manage Members',
  [Permission.ManageRoles]:     'Manage Roles',
  [Permission.InviteMembers]:   'Invite Members',
  [Permission.ManageMetadata]:  'Manage Metadata',
  // Work Items
  [Permission.CreateWorkItem]:  'Create Work Item',
  [Permission.EditWorkItem]:    'Edit Work Item',
  [Permission.DeleteWorkItem]:  'Delete Work Item',
  [Permission.AssignWorkItem]:  'Assign Work Item',
  // Backlog
  [Permission.ManageBacklog]:   'Manage Backlog',
  [Permission.PromoteToSprint]: 'Promote to Sprint',
  // Sprint
  [Permission.ManageSprint]:    'Manage Sprint',
  [Permission.ActivateSprint]:  'Activate Sprint',
  // Capacity / Board
  [Permission.ManageCapacity]:  'Manage Capacity',
  [Permission.ManageBoard]:     'Manage Board',
  // Analytics & Integrations
  [Permission.ViewAnalytics]:   'View Analytics',
  [Permission.ManagePipeline]:  'Manage Pipeline',
  [Permission.ManageRepo]:      'Manage Repo',
  // Collaboration (HUB)
  [Permission.ManageChannels]:  'Manage Channels',
};

/** Permissions grouped by functional domain — used to render the role-editor checkbox grid. */
export const PERMISSION_GROUPS: { label: string; permissions: Permission[] }[] = [
  {
    label: 'Repository',
    permissions: [Permission.ViewRepository, Permission.EditRepository, Permission.ManageRepo],
  },
  {
    label: 'Members & Access',
    permissions: [Permission.ManageMembers, Permission.ManageRoles, Permission.InviteMembers, Permission.ManageMetadata],
  },
  {
    label: 'Work Items',
    permissions: [Permission.CreateWorkItem, Permission.EditWorkItem, Permission.DeleteWorkItem, Permission.AssignWorkItem],
  },
  {
    label: 'Backlog',
    permissions: [Permission.ManageBacklog, Permission.PromoteToSprint],
  },
  {
    label: 'Sprint',
    permissions: [Permission.ManageSprint, Permission.ActivateSprint, Permission.ManageCapacity],
  },
  {
    label: 'Board & Analytics',
    permissions: [Permission.ManageBoard, Permission.ViewAnalytics, Permission.ManagePipeline],
  },
  {
    label: 'Collaboration',
    permissions: [Permission.ManageChannels],
  },
];

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

/** Type identifier colour (Nexus UI: colour only carries meaning — the type). */
export const WORK_ITEM_TYPE_COLOR: Record<WorkItemType, string> = {
  'user-story': 'text-type-story',
  bug:          'text-type-bug',
  task:         'text-type-task',
  improvement:  'text-type-improvement',
  'test-plan':  'text-type-test',
};

/** Icon tile behind a type icon — neutral; the icon alone carries the type colour. */
export const WORK_ITEM_TYPE_RING: Record<WorkItemType, string> = {
  'user-story': 'ring-slate-800 bg-slate-800',
  bug:          'ring-slate-800 bg-slate-800',
  task:         'ring-slate-800 bg-slate-800',
  improvement:  'ring-slate-800 bg-slate-800',
  'test-plan':  'ring-slate-800 bg-slate-800',
};

/** 8px type square shown next to a work-item ID (see .nx-type in styles.css). */
export const WORK_ITEM_TYPE_SQUARE: Record<WorkItemType, string> = {
  'user-story': 'bg-type-story',
  bug:          'bg-type-bug',
  task:         'bg-type-task',
  improvement:  'bg-type-improvement',
  'test-plan':  'bg-type-test',
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

/** State indicator per SprintTaskApiState — 6px dot + plain text (Nexus UI: no filled pills). */
export const SPRINT_TASK_STATE_BADGE: Record<SprintTaskApiState, string> = {
  [SprintTaskApiState.New]:      'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-slate-500',
  [SprintTaskApiState.Backlog]:  'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-slate-500',
  [SprintTaskApiState.Todo]:     'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-slate-500',
  [SprintTaskApiState.Active]:   'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-blue-500',
  [SprintTaskApiState.InReview]: 'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-amber-500',
  [SprintTaskApiState.Done]:     'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
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

/** State indicator per WorkItemApiState — 6px dot + plain text (Nexus UI: no filled pills). */
export const STATE_BADGE_CLASSES: Record<WorkItemApiState, string> = {
  [WorkItemApiState.New]:           'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-slate-500',
  [WorkItemApiState.Active]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-blue-500',
  [WorkItemApiState.Closed]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.Completed]:     'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.Resolved]:      'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.Reopen]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-amber-500',
  [WorkItemApiState.InTest]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-amber-500',
  [WorkItemApiState.DoneTest]:      'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.DevCompleted]:  'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.TestCompleted]: 'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.Design]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-slate-500',
  [WorkItemApiState.Review]:        'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-amber-500',
  [WorkItemApiState.Run]:           'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-blue-500',
  [WorkItemApiState.Pass]:          'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-emerald-500',
  [WorkItemApiState.Fail]:          'bg-transparent text-slate-200 ring-transparent before:mr-1.5 before:inline-block before:h-1.5 before:w-1.5 before:shrink-0 before:rounded-full before:content-[""] before:bg-rose-500',
};
