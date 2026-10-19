/** Link types — must match backend WorkItemLinkType enum (string names). */
export type WorkItemLinkType =
  | 'Related'
  | 'Blocks'
  | 'Duplicate'
  | 'DependsOn'
  | 'Successor'
  | 'Affects'
  | 'Tests'
  | 'CausedBy';

/** A link as seen from one work item, returned by GET .../links. */
export interface WorkItemLinkDto {
  id: string;
  linkType: WorkItemLinkType;
  label: string;
  targetTaskId: string;
  targetKey: string;
  targetTitle: string;
  targetType: string;
  targetState: string;
  description: string | null;
}

/** Numeric value of each link type — must match backend WorkItemLinkType enum order. */
export const LINK_TYPE_VALUE: Record<WorkItemLinkType, number> = {
  Related:   0,
  Blocks:    1,
  Duplicate: 2,
  DependsOn: 3,
  Successor: 4,
  Affects:   5,
  Tests:     6,
  CausedBy:  7,
};

/** The link-type options shown in the "Add link" dialog (outward phrasing). */
export const LINK_TYPE_OPTIONS: ReadonlyArray<{ value: WorkItemLinkType; label: string }> = [
  { value: 'Related',   label: 'Related to' },
  { value: 'Blocks',    label: 'Blocks' },
  { value: 'DependsOn', label: 'Depends on' },
  { value: 'Duplicate', label: 'Duplicate of' },
  { value: 'Successor', label: 'Successor of' },
  { value: 'Affects',   label: 'Affects' },
  { value: 'Tests',     label: 'Tests' },
  { value: 'CausedBy',  label: 'Caused by' },
];
