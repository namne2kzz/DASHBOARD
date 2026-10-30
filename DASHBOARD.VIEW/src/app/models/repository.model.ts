/** Matches RepositoryDto from the backend. */
export interface RepositoryApiDto {
  id: string;
  name: string;
  /** Short code. Forms the URL segment and work-item key prefix, so it is never editable. */
  code: string;
  description: string;
  memberCount: number;
  createdAt: string;
  /** Archived repositories are hidden from ordinary views but kept intact and restorable. */
  isArchived: boolean;
  /** The Project Manager's user id, or null when nobody holds the role. */
  leadId: string | null;
  /** The Project Manager's display name, or null when nobody holds the role. */
  leadName: string | null;
}

/** Payload for POST /api/repositories. */
export interface CreateRepositoryPayload {
  name: string;
  code: string;
  description: string;
  /** The user who leads the repository; seeded as its first member with the Project Manager role. */
  projectManagerId: string;
}

/**
 * Payload for PUT /api/repositories/{id}.
 *
 * `code` is absent on purpose — the backend will not change it, because every shared link and
 * work-item key is built from it.
 */
export interface UpdateRepositoryPayload {
  name: string;
  description: string;
  /** Omit to leave the current Project Manager in place; set to hand the role over. */
  leadId?: string;
}
