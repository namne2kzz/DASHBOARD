import type { SprintTaskApiType, WorkItemApiPriority } from '../core/enums/system.enum';

export type LoadState = 'safe' | 'warning' | 'overloaded';

export type SprintStatus = 'Planning' | 'Active' | 'Closed';

export interface Sprint {
  id:             string;
  name:           string;
  startDate:      string;
  endDate:        string;
  isActive:       boolean;
  status:         SprintStatus;
  closedAt?:      string | null;
  /** HUB Chat channel linked to this sprint, or null if none. */
  hubChannelId?:  string | null;
  /** Full URL to open the linked HUB channel in the browser. */
  hubChannelUrl?: string | null;
}

export interface CloseSprintResult {
  closed:             boolean;
  incompleteItemCount: number;
  warning?:           string | null;
}

export interface SprintTask {
  id:               string;
  parentId:         string | null;
  type:             'user-story' | 'task' | 'bug' | 'test-plan';
  workItemNumber:   string;
  title:            string;
  description:      string;
  priority:         WorkItemApiPriority;
  assignedToId:     string | null;
  assignedToName:   string | null;
  state:            'open' | 'todo' | 'in-progress' | 'in-review' | 'verified' | 'running' | 'done' | 'passed' | 'failed' | 'closed';
  storyPoints:        number;
  originalEstimate:   number;
  remainingWork:      number;
  completedWork:      number;
  // UserStory-specific
  acceptanceCriteria: string | null;
  documents:          string[];
}

export interface MemberLoad {
  userId:               string;
  name:                 string;
  role:                 string;
  hoursPerDay:          number;
  overtimeHoursPerDay:  number;
  personalDaysOff:      number;
  capacity:             number;
  workload:             number;
  loadPercent:          number;
  state:                LoadState;
}

export interface AddSprintTaskDialogData {
  storyId:    string;
  storyTitle: string;
}

export interface SprintStoryDetailDialogData {
  story:    SprintTask;
  subTasks: SprintTask[];
}

export interface CreateWorkItemDialogData {
  /** Work item type chosen from the "New Workitem" menu — fixed for the dialog's lifetime. */
  type: SprintTaskApiType;
}
