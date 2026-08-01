import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { WorkItemMetadataDto } from '../models/work-item-metadata.model';

/** Reads and replaces the metadata catalog values assigned to a work item. */
@Injectable({ providedIn: 'root' })
export class WorkItemMetadataService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  /** @param taskId Work item ID. @returns The metadata values currently assigned to the task. */
  get(taskId: string): Observable<WorkItemMetadataDto[]> {
    return this.http.get<WorkItemMetadataDto[]>(`${this.base(taskId)}/metadata`);
  }

  /**
   * Replaces the full set of assigned metadata values.
   * @param taskId Work item ID.
   * @param metadataIds Complete desired set of catalog value IDs.
   * @returns Observable completing on success (204).
   */
  set(taskId: string, metadataIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.base(taskId)}/metadata`, { metadataIds });
  }

  private base(taskId: string): string {
    const repoId = this.repoCtx.selectedRepoId();
    return `${environment.apiBaseUrl}/repositories/${repoId}/sprint-tasks/${taskId}`;
  }
}
