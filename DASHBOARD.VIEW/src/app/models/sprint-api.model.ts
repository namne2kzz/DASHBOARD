/** Sprint snapshot returned by GET /api/repositories/{repoId}/sprints. */
export interface SprintApiDto {
  id: string;
  repositoryId: string;
  name: string;
  /** ISO date string e.g. "2026-05-04" (DateOnly serialised by .NET). */
  startDate: string;
  /** ISO date string e.g. "2026-05-15". */
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

/** Sprint capacity/velocity summary returned by GET /api/.../sprints/{id}/summary. */
export interface SprintSummaryApiDto {
  sprintId: string;
  sprintName: string;
  totalWorkingDays: number;
  totalCapacityHours: number;
  committedStoryPoints: number;
  completedStoryPoints: number;
  totalTasks: number;
  completedTasks: number;
}
