import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { HttpCacheService } from './http-cache.service';
import { RepositoryContextService } from './repository-context.service';
import { WorkItemMetadataDto } from '../models/work-item-metadata.model';

/** Reads and replaces the metadata catalog values assigned to a work item. */
@Injectable({ providedIn: 'root' })
export class WorkItemMetadataService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly cache   = inject(HttpCacheService);

  /** How long an assigned-metadata read stays fresh; only this service's own set() changes it. */
  private static readonly TtlMs = 60_000;

  /** @param taskId Work item ID. @returns The metadata values currently assigned to the task. */
  get(taskId: string): Observable<WorkItemMetadataDto[]> {
    return this.cache.get(
      this.cacheKey(taskId),
      WorkItemMetadataService.TtlMs,
      () => this.http.get<WorkItemMetadataDto[]>(`${this.base(taskId)}/metadata`));
  }

  /**
   * Replaces the full set of assigned metadata values.
   * @param taskId Work item ID.
   * @param metadataIds Complete desired set of catalog value IDs.
   * @returns Observable completing on success (204).
   */
  set(taskId: string, metadataIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.base(taskId)}/metadata`, { metadataIds }).pipe(
      // Without this the editor would reopen showing the labels as they were before the save.
      tap(() => this.cache.invalidate(this.cacheKey(taskId))),
    );
  }

  /** Cache key for one task's assigned metadata, scoped by repository. @param taskId Work item ID. */
  private cacheKey(taskId: string): string {
    return `wi-metadata:${this.repoCtx.selectedRepoId()}:${taskId}`;
  }

  private base(taskId: string): string {
    const repoId = this.repoCtx.selectedRepoId();
    return `${environment.apiBaseUrl}/repositories/${repoId}/sprint-tasks/${taskId}`;
  }
}
