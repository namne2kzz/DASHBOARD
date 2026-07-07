import { WorkItemApiPriority, SprintTaskApiType, SprintTaskApiState } from '../core/enums/system.enum';

export { SprintTaskApiType, SprintTaskApiState, WorkItemApiPriority };

/** Minimal sprint summary from GET /sprints. */
export interface SprintApiDto {
  id:           string;
  repositoryId: string;
  name:         string;
  startDate:    string;
  endDate:      string;
  isActive:     boolean;
  createdAt:    string;
}

/** Full work item node (tree structure via subTasks). */
export interface SprintTaskApiDto {
  id:               string;
  sprintId:         string | null;
  repositoryId:     string;
  backlogItemId:    string | null;
  parentId:         string | null;
  /** Formatted work item number of the parent User Story (e.g. "DASH-3"), or null if no parent. */
  parentWorkItemNumber: string | null;
  /** Title of the parent User Story, or null if no parent. */
  parentTitle:      string | null;
  /** Formatted work item number (e.g. "DASH-3") — repo code prefix added at read time. */
  workItemNumber:   string;
  type:             SprintTaskApiType;
  title:            string;
  description:      string;
  priority:         WorkItemApiPriority;
  assignedToId:     string | null;
  assignedToName:   string | null;
  assignedToAvatar: string | null;
  state:            SprintTaskApiState;
  storyPoints:      number;
  originalEstimate: number;
  remainingWork:    number;
  completedWork:    number;
  closedAt:         string | null;
  // Bug fields
  stepsToReproduce: string | null;
  environment:      string | null;
  rootCause:        string | null;
  solution:         string | null;
  impaction:        string | null;
  // Task + Bug shared
  unitTest:         string | null;
  designReview:     string | null;
  // TestPlan fields
  testSteps:        string[] | null;
  automated:        boolean | null;
  subTasks:         SprintTaskApiDto[];
}

/** Payload for POST /sprints/{id}/tasks — type-specific fields are null when not applicable. */
export interface CreateWorkItemPayload {
  type:             SprintTaskApiType;
  title:            string;
  description:      string;
  priority:         WorkItemApiPriority;
  assignedToId:     string | null;
  /** Parent User Story ID, or null for a root-level item. */
  parentId:         string | null;
  originalEstimate: number;
  stepsToReproduce: string | null;
  environment:      string | null;
  rootCause:        string | null;
  solution:         string | null;
  impaction:        string | null;
  unitTest:         string | null;
  designReview:     string | null;
  testSteps:        string[] | null;
  automated:        boolean | null;
}

/** Minimal projection for the parent-picker search dropdown. */
export interface WorkItemPickerApiDto {
  id:             string;
  workItemNumber: string;
  title:          string;
}

/** Flat board task item returned by GET /sprints/{id}/tasks/board — no tree hierarchy. */
export interface BoardTaskApiDto {
  id:               string;
  workItemNumber:   string;
  type:             SprintTaskApiType;
  title:            string;
  priority:         WorkItemApiPriority;
  assignedToId:     string | null;
  assignedToName:   string | null;
  assignedToAvatar: string | null;
  state:            SprintTaskApiState;
  originalEstimate: number;
  remainingWork:    number;
  completedWork:    number;
  /** UTC ISO-8601 timestamp of last state transition; null for legacy items. */
  stateChangedAt:   string | null;
}

/** Lightweight summary for list views. */
export interface SprintTaskSummaryApiDto {
  id:               string;
  sprintId:         string | null;
  repositoryId:     string;
  workItemNumber:   string;
  type:             SprintTaskApiType;
  title:            string;
  priority:         WorkItemApiPriority;
  assignedToId:     string | null;
  assignedToName:   string | null;
  assignedToAvatar: string | null;
  state:            SprintTaskApiState;
  storyPoints:      number;
  originalEstimate: number;
  remainingWork:    number;
  subTaskCount:     number;
}

/** Capacity configuration for one member. */
export interface CapacityMemberApiDto {
  id:                   string;
  sprintId:             string;
  userId:               string;
  userName:             string;
  userAvatar:           string;
  role:                 string;
  hoursPerDay:          number;
  overtimeHoursPerDay:  number;
}

/** Day-off entry (userId null = team-wide). */
export interface DayOffApiDto {
  id:       string;
  sprintId: string;
  userId:   string | null;
  userName: string | null;
  date:     string;
  hours:    number;
  reason:   string;
}

/** Server-computed capacity vs workload for one member. */
export interface MemberLoadApiDto {
  userId:               string;
  userName:             string;
  userAvatar:           string;
  hoursPerDay:          number;
  overtimeHoursPerDay:  number;
  personalDaysOffHours: number;
  capacity:             number;
  workload:             number;
  loadPercent:          number;
  loadState:            'safe' | 'warning' | 'overloaded';
}

/** Full sprint planning snapshot from GET /sprints/{id}/detail. */
export interface SprintDetailApiDto {
  id:              string;
  repositoryId:    string;
  name:            string;
  startDate:       string;
  endDate:         string;
  isActive:        boolean;
  workingDays:     number;
  tasks:           SprintTaskApiDto[];
  capacityMembers: CapacityMemberApiDto[];
  daysOff:         DayOffApiDto[];
  memberLoads:     MemberLoadApiDto[];
}
