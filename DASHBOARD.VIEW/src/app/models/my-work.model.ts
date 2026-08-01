import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../core/enums/system.enum';

/** A single open work item assigned to the current user, as returned by GET /api/my-work. */
export interface MyWorkItemApiDto {
  id: string;
  repositoryId: string;
  repositoryCode: string;
  repositoryName: string;
  sprintId: string | null;
  sprintName: string | null;
  workItemNumber: string;
  type: SprintTaskApiType;
  title: string;
  priority: WorkItemApiPriority;
  state: SprintTaskApiState;
  storyPoints: number;
  remainingWork: number;
}

/** Dimension the "My Work" list can be grouped by. */
export type MyWorkGrouping = 'sprint' | 'priority' | 'state';

/** A named bucket of work items produced by grouping the flat list. */
export interface MyWorkGroup {
  key: string;
  label: string;
  items: MyWorkItemApiDto[];
}
