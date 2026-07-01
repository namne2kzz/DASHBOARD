export type WipMode = 'soft' | 'hard';

/** Sprint-task state key — matches the string values used in SprintTask.state. */
export type SprintTaskStateKey = 'new' | 'backlog' | 'todo' | 'active' | 'in-review' | 'done';

export interface BoardColumn {
  id: string;
  name: string;
  /** The sprint-task state whose items appear in this column. */
  mappedState: SprintTaskStateKey;
  wipLimit: number;
  wipMode: WipMode;
  agingLimitDays: number;
}

export interface NewColumnForm {
  name: string;
  mappedState: SprintTaskStateKey;
  wipLimit: number;
  wipMode: WipMode;
  agingLimitDays: number;
}
