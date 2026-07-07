/** A single discussion comment on a sprint task. */
export interface DiscussionApiDto {
  id:           string;
  sprintTaskId: string;
  authorId:     string;
  authorName:   string;
  authorAvatar: string;
  body:         string;
  createdAt:    string;
  updatedAt:    string | null;
}

/** A single audit-trail entry for a sprint task. */
export interface HistoryApiDto {
  id:           string;
  sprintTaskId: string;
  authorId:     string;
  authorName:   string;
  authorAvatar: string;
  message:      string;
  createdAt:    string;
}

/** Payload for PUT /sprint-tasks/{taskId} — excludes state and assignee, which have their own endpoints. */
export interface UpdateSprintTaskPayload {
  title:            string;
  description:      string;
  priority:         number;
  assignedToId:     string | null;
  storyPoints:      number;
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
  /** New parent User Story id; null to remove. */
  parentId:         string | null;
}
