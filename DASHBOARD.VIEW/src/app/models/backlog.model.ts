/** Hierarchy level used in the backlog view (maps to BacklogItemApiType). */
export type BacklogLevel = 'epic' | 'feature' | 'user-story';

/** Estimation mode displayed in the backlog refinement table. */
export type EstimateMode = 'fibonacci' | 'tshirt';

/** T-shirt size label (view layer, maps to BacklogTshirtSize). */
export type TshirtSize = 'XS' | 'S' | 'M' | 'L' | 'XL';

/** Refinement state label (view layer, maps to BacklogItemApiState). */
export type BacklogState = 'new' | 'refining' | 'ready' | 'committed';

/** Data injected into BacklogAcDialogComponent via DIALOG_DATA. */
export interface BacklogAcDialogData {
  itemId: string;
  acceptanceCriteria: string[];
}

/** Data injected into BacklogDocumentsDialogComponent via DIALOG_DATA. */
export interface BacklogDocumentsDialogData {
  itemId: string;
  documents: string[];
}

/** Flat view-model used by BacklogManagementService signals and the template. */
export interface BacklogItem {
  id: string;
  type: BacklogLevel;
  title: string;
  parentId: string | null;
  rank: number;
  sprintId: string | null;
  sprintName: string | null;
  state: BacklogState;
  storyPoints: number | null;
  tshirtSize: TshirtSize | null;
  acceptanceCriteria: string[];
  documents: string[];
}
