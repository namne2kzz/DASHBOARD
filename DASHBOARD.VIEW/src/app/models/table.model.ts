export interface ColumnDef<T> {
  key: keyof T & string;
  header: string;
  sortable?: boolean;
  width?: string;
  cellClass?: string;
  headerClass?: string;
}

export type SortDirection = 'asc' | 'desc' | null;

export interface SortState<T> {
  key: keyof T & string;
  direction: SortDirection;
}
