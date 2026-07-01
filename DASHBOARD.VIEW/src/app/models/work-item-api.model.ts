import { WorkItemApiType, WorkItemApiPriority, WorkItemApiState } from '../core/enums/system.enum';
export { WorkItemApiType, WorkItemApiPriority, WorkItemApiState };

/** Matches WorkItemSummaryDto from the backend (list view). */
export interface WorkItemSummaryApiDto {
  id: string;
  workItemNumber: number;
  customId: string;
  workItemType: WorkItemApiType;
  title: string;
  priority: WorkItemApiPriority;
  state: WorkItemApiState;
  assignedToId: string | null;
  assignedToName: string | null;
  assignedToAvatar: string | null;
  sprint: string;
  createdAt: string;
}

/** Matches WorkItemDto from the backend (full detail). */
export interface WorkItemDetailApiDto extends WorkItemSummaryApiDto {
  repositoryId: string;
  description: string;
  implementInBuild: string | null;
  fixedInVersion: string | null;
  links: string[];
  closedAt: string | null;
  updatedAt: string | null;
  acceptanceCriteria: string | null;
  storyPoints: number | null;
  stepsToReproduce: string | null;
  environment: string | null;
  rootCause: string | null;
  solution: string | null;
  impaction: string | null;
  unitTest: string | null;
  designReview: string | null;
  originalEstimate: number | null;
  remainingWork: number | null;
  completedWork: number | null;
  testSuiteId: string | null;
  automated: boolean | null;
  testSteps: string[] | null;
}

/** Matches HistoryDto from the backend. */
export interface HistoryApiDto {
  id: string;
  workItemId: string;
  authorId: string;
  authorName: string;
  authorAvatar: string;
  message: string;
  createdAt: string;
}

/** Matches DiscussionDto from the backend. */
export interface DiscussionApiDto {
  id: string;
  workItemId: string;
  authorId: string;
  authorName: string;
  authorAvatar: string;
  body: string;
  createdAt: string;
  updatedAt: string | null;
}

