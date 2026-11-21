/** A single discussion comment on a sprint task. */
export interface DiscussionApiDto {
  id:           string;
  sprintTaskId: string;
  /** Root comment this replies to; null when it is itself a root comment. */
  parentId:     string | null;
  authorId:     string;
  authorName:   string;
  authorAvatar: string;
  body:         string;
  createdAt:    string;
  updatedAt:    string | null;
  /** Total replies to this comment; always 0 on a reply. */
  replyCount:   number;
  /** Whether the signed-in user may edit this comment. */
  canEdit:      boolean;
  /** Whether the signed-in user may delete this comment. */
  canDelete:    boolean;
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
  /** Acceptance criteria serialised as a JSON array string (UserStory only); null for other types. */
  acceptanceCriteria: string | null;
  /** Document URLs (UserStory only); null for other types. */
  documents:          string[] | null;
  /** Remaining work hours (Task/Bug — updated by the user; for other types pass 0). */
  remainingWork:      number;
}

/**
 * Which draft a pasted or dropped image should be inserted into.
 *
 * The work-item page has four Markdown editors — description, new comment, reply and edit — and the
 * upload handler needs to be told which one it is serving.
 */
export type EditorTarget = 'description' | 'comment' | 'reply' | 'edit';
