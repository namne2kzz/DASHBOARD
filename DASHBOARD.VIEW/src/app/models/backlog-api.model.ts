import { BacklogItemApiType, BacklogItemApiState, BacklogTshirtSize } from '../core/enums/system.enum';
export { BacklogItemApiType, BacklogItemApiState, BacklogTshirtSize };

/** Matches BacklogItemDto returned by GET /api/repositories/{repoId}/backlog. */
export interface BacklogItemApiDto {
  id: string;
  repositoryId: string;
  type: BacklogItemApiType;
  title: string;
  parentId: string | null;
  rank: number;
  sprintId: string | null;
  sprintName: string | null;
  state: BacklogItemApiState;
  storyPoints: number | null;
  tshirtSize: BacklogTshirtSize | null;
  acceptanceCriteria: string;
  documents: string[];
  createdAt: string;
  children: BacklogItemApiDto[];
}

/** Request body for POST /api/repositories/{repoId}/backlog. */
export interface CreateBacklogItemApiRequest {
  type: BacklogItemApiType;
  title: string;
  parentId?: string | null;
  storyPoints?: number | null;
  tshirtSize?: BacklogTshirtSize | null;
  acceptanceCriteria: string;
}

/** Request body for PUT /api/repositories/{repoId}/backlog/{id}. */
export interface UpdateBacklogItemApiRequest {
  title: string;
  state: BacklogItemApiState;
  sprintId?: string | null;
  storyPoints?: number | null;
  tshirtSize?: BacklogTshirtSize | null;
  acceptanceCriteria: string;
  documents: string[];
}

/** Request body for PATCH /api/repositories/{repoId}/backlog/{id}/rank. */
export interface RankBacklogItemApiRequest {
  previousItemId: string | null;
  nextItemId: string | null;
}

/** Request body for PATCH /api/repositories/{repoId}/backlog/{id}/iteration. */
export interface MoveToIterationApiRequest {
  sprintId: string | null;
}

/** Sprint summary returned by GET /api/repositories/{repoId}/sprints. */
export interface SprintApiDto {
  id: string;
  name: string;
  isActive: boolean;
  startDate: string;
  endDate: string;
}

/** Request body for PATCH /api/repositories/{repoId}/backlog/{id}/documents. */
export interface UpdateBacklogDocumentsApiRequest {
  documents: string[];
}
