import type { SprintTaskApiType, WorkItemApiPriority } from '../core/enums/system.enum';

export type LoadState = 'safe' | 'warning' | 'overloaded';

export interface Sprint {
  id:        string;
  name:      string;
  startDate: string;
  endDate:   string;
  isActive:  boolean;
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
  state:            'new' | 'backlog' | 'todo' | 'active' | 'in-review' | 'done';
  storyPoints:      number;
  originalEstimate: number;
  remainingWork:    number;
  completedWork:    number;
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
