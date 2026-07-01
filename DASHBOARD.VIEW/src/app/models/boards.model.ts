import { SprintTaskApiState, SprintTaskApiType } from '../core/enums/system.enum';

export type BoardColumnState = 'todo' | 'in-progress' | 'done';

export type BoardItemType = 'user-story' | 'task' | 'bug' | 'test-plan';

export type BoardPriority = 'low' | 'medium' | 'high' | 'critical';

export interface DiscussionEntry {
  id:          string;
  authorId:    string;
  authorName?: string;
  body:        string;
  createdAt:   string;
}

export interface HistoryEntry {
  id:           string;
  authorId:     string;
  authorName:   string;
  authorAvatar: string;
  message:      string;
  createdAt:    string;
}

/** A work item displayed on the boards page (Bug, Task, TestPlan). */
export interface BoardItem {
  id:               string;
  /** Formatted work item number (e.g. "DASH-3"). */
  workItemNumber:   string;
  type:             BoardItemType;
  title:            string;
  description:      string;
  priority:         BoardPriority;
  columnState:      BoardColumnState;
  apiState:         SprintTaskApiState;
  apiType:          SprintTaskApiType;
  assignedToId:     string | null;
  assignedToName:   string | null;
  assignedToAvatar: string | null;
  // Bug fields
  stepsToReproduce: string | null;
  environment:      string | null;
  rootCause:        string | null;
  solution:         string | null;
  impaction:        string | null;
  // Task + Bug shared
  unitTest:         string | null;
  designReview:     string | null;
  originalEstimate: number;
  remainingWork:    number;
  completedWork:    number;
  // TestPlan fields
  testSteps:        string[] | null;
  automated:        boolean | null;
  // Meta
  discussions:      DiscussionEntry[];
  history:          HistoryEntry[];
  /** UTC ISO-8601 timestamp of the last state transition; null for legacy items. */
  stateChangedAt:   string | null;
}
