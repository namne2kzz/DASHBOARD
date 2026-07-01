/** Aggregated overview statistics returned by GET /api/repositories/{repoId}/overview. */
export interface OverviewStatsDto {
  workItemCounts: WorkItemCountsDto;
  statusDistribution: StatusDistributionDto;
  typeDistribution: TypeDistributionItemDto[];
  availableSprints: SprintRefDto[];
  burndownData: BurndownDataDto;
  storyPoints: StoryPointsDto;
  velocityTrend: VelocityTrendItemDto[];
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
