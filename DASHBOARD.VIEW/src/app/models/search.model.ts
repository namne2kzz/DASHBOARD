/** Entity kind a global-search hit belongs to. */
export type SearchResultKind = 'task' | 'backlog';

/** A single normalized global-search hit returned by GET /repositories/{id}/search. */
export interface SearchResultItem {
  kind: SearchResultKind;
  id: string;
  title: string;
  subtitle: string | null;
}

/** A named bucket of search hits of the same kind. */
export interface SearchResultGroup {
  kind: SearchResultKind;
  label: string;
  items: SearchResultItem[];
}
