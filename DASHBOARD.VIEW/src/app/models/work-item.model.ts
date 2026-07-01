import { WorkItemApiState } from '../core/enums/system.enum';
export { WorkItemApiState };


export type WorkItemStatus = 'todo' | 'in-progress' | 'done';

export type WorkItemPriority = 'critical' | 'high' | 'medium' | 'low';

/** Azure DevOps–style work item kinds */
export type WorkItemType = 'user-story' | 'bug' | 'task' | 'improvement' | 'test-plan';

export interface DiscussionEntry {
  id: string;
  authorId: string;
  /** Display name returned by the API — avoids a secondary user lookup. */
  authorName?: string;
  body: string;
  createdAt: string;
}

export interface HistoryEntry {
  id: string;
  authorId: string;
  authorName: string;
  authorAvatar: string;
  message: string;
  createdAt: string;
}

export interface WorkItem {
  id: string;
  workItemNumber: number;
  customId: string;
  title: string;
  description: string;
  priority: WorkItemPriority;
  status: WorkItemStatus;
  state: WorkItemApiState;
  workItemType: WorkItemType;
  assignedToId: string | null;
  sprint: string;
  implementInBuild: string | null;
  fixedInVersion: string | null;
  wikiLinks: string[];

  // ── UserStory ────────────────────────────────────────
  acceptanceCriteria: string | null;
  storyPoints: number | null;

  // ── Bug ──────────────────────────────────────────────
  stepsToReproduce: string | null;
  environment: string | null;
  rootCause: string | null;
  solution: string | null;
  impaction: string | null;

  // ── Bug + Task ────────────────────────────────────────
  unitTest: string | null;
  designReview: string | null;

  // ── Task ──────────────────────────────────────────────
  originalEstimate: number | null;
  remainingWork: number | null;
  completedWork: number | null;

  // ── TestPlan ──────────────────────────────────────────
  automated: boolean | null;
  testSteps: string[] | null;

  discussions: DiscussionEntry[];
  history: HistoryEntry[];
}

/** A single step in the horizontal assignee progression strip. */
export interface AssigneeTimelineEntry {
  /** Display name of the assignee; null means unassigned. */
  assigneeName: string | null;
  /** Author who made the assignment change; absent for the initial state. */
  authorName?: string;
  /** ISO timestamp of the change; absent for the initial state. */
  createdAt?: string;
}

/** A single step in the horizontal state progression strip. */
export interface StateTimelineEntry {
  state: WorkItemApiState;
  /** Author who triggered the transition; absent for the initial state. */
  authorName?: string;
  /** ISO timestamp of the transition; absent for the initial state. */
  createdAt?: string;
}

