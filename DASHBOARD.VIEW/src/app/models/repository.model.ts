/** Matches RepositoryDto from the backend. */
export interface RepositoryApiDto {
  id: string;
  name: string;
  code: string;
  description: string;
  memberCount: number;
  createdAt: string;
}

/** Payload for POST /api/repositories. */
export interface CreateRepositoryPayload {
  name: string;
  code: string;
  description: string;
  scrumMasterId: string;
}
