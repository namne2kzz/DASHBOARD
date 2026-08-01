/** Derived category of an audit-log entry. */
export type AuditLogCategory = 'created' | 'state' | 'assignment' | 'update';

/** A single repository audit-log row from GET /repositories/{id}/audit-log. */
export interface AuditLogEntry {
  id: string;
  sprintTaskId: string;
  workItemNumber: string;
  authorId: string;
  authorName: string;
  authorAvatar: string;
  message: string;
  category: AuditLogCategory;
  createdAt: string;
}

/** Generic paged response envelope matching the backend PagedResult<T>. */
export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** Active audit-log filter selection. */
export interface AuditLogFilters {
  authorId: string;
  from: string;
  to: string;
  category: AuditLogCategory | '';
}
