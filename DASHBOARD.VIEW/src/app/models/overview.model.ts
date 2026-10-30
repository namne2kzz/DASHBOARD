/** Aggregated overview statistics returned by GET /api/repositories/{repoId}/overview. */
export interface OverviewStatsDto {
  workItemCounts: WorkItemCountsDto;
  statusDistribution: StatusDistributionDto;
  typeDistribution: TypeDistributionItemDto[];
  availableSprints: SprintRefDto[];
  burndownData: BurndownDataDto;
  storyPoints: StoryPointsDto;
  velocityTrend: VelocityTrendItemDto[];
  typeTrend: SprintTypeTrendItemDto[];
  sprintHealth: SprintHealthDto;
  cycleTime: CycleTimeDto;
  assigneeWorkload: AssigneeWorkloadItemDto[];
  recentActivity: RecentActivityItemDto[];
  report: SprintReportDto;
  attention: AttentionItemDto[];
  comparison: SprintComparisonDto;
}

/** What an attention item is about — decides its icon and where clicking it leads. */
export type AttentionKind = 'WipExceeded' | 'Overloaded' | 'StaleItem';

/** How loudly an attention item is shown. */
export type AttentionSeverity = 'Warning' | 'Critical';

/** One problem on the board worth acting on, pre-judged by the server. */
export interface AttentionItemDto {
  kind: AttentionKind;
  severity: AttentionSeverity;
  /** Ready-to-display summary, e.g. "Code Review over WIP · 25/3". */
  message: string;
  /** Repo-relative route to the screen that resolves it. */
  route: string;
  /** Column, member or task the destination should focus on. */
  targetId: string | null;
}

/** The previous sprint's headline figures, for showing change rather than a bare number. */
export interface SprintComparisonDto {
  /** False on a repository's first sprint; every delta is then hidden. */
  hasPrevious: boolean;
  completedPoints: number;
  cycleTimeDays: number;
  totalItems: number;
  doneItems: number;
}

/** Sprint report card — completion and scope-change summary for the selected sprint. */
export interface SprintReportDto {
  totalTasks: number;
  completedTasks: number;
  taskCompletionRate: number;
  pointCompletionRate: number;
  addedAfterStart: number;
  scopeChangePoints: number;
}

/** Work item counts by type for the selected sprint. */
export interface WorkItemCountsDto {
  total: number;
  bugs: number;
  userStories: number;
  tasks: number;
  testPlans: number;
}

/** Item counts per workflow state for the selected sprint. */
export interface StatusDistributionDto {
  todo: number;
  active: number;
  inReview: number;
  done: number;
  new: number;
  backlog: number;
}

/** Work item count for one SprintTaskType. */
export interface TypeDistributionItemDto {
  type: string;
  count: number;
}

/** Sprint identifier + display name for the sprint selector. */
export interface SprintRefDto {
  id: string;
  name: string;
  /** ISO date, e.g. "2026-06-29". */
  startDate: string;
  /** ISO date, e.g. "2026-07-10". */
  endDate: string;
}

/** Burndown chart data — one element per sprint working day. */
export interface BurndownDataDto {
  ideal: number[];
  actual: number[];
}

/** Committed vs completed story points for the selected sprint. */
export interface StoryPointsDto {
  committed: number;
  completed: number;
}

/** Per-sprint velocity entry for the trend chart. */
export interface VelocityTrendItemDto {
  sprintName: string;
  committedPoints: number;
  completedPoints: number;
}

/** Work item counts by type for one sprint in the type-trend chart. */
export interface SprintTypeTrendItemDto {
  sprintName: string;
  userStory: number;
  task: number;
  bug: number;
  testPlan: number;
}

/** On-track/at-risk/behind indicator for the selected sprint. */
export interface SprintHealthDto {
  status: 'OnTrack' | 'AtRisk' | 'Behind';
  daysRemaining: number;
  idealRemaining: number;
  actualRemaining: number;
}

/** Approximate average cycle time for completed items in the selected sprint. */
export interface CycleTimeDto {
  averageDays: number;
  sampleCount: number;
}

/** Task/story-point workload for one assignee in the selected sprint. */
export interface AssigneeWorkloadItemDto {
  assigneeId: string | null;
  assigneeName: string;
  avatarClass: string | null;
  taskCount: number;
  storyPoints: number;
}

/** One audit-trail entry for the repository-wide recent activity feed. */
export interface RecentActivityItemDto {
  sprintTaskId: string;
  taskTitle: string;
  message: string;
  authorName: string;
  avatarClass: string | null;
  createdAt: string;
}
