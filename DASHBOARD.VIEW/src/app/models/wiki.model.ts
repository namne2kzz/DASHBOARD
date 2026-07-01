export interface WikiPage {
  id: string;
  title: string;
  content: string;
  lastUpdated: string;
  /** Optional parent for tree grouping (design doc hierarchy) */
  parentId?: string | null;
}
