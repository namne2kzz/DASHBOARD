import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import {
  MetadataDto,
  MetadataKeyOption,
  CreateMetadataPayload,
  UpdateMetadataPayload,
} from '../models/metadata.model';

@Injectable({ providedIn: 'root' })
export class MetadataService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  readonly items   = signal<MetadataDto[]>([]);
  readonly keys    = signal<MetadataKeyOption[]>([]);
  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (repoId) {
        this.load(repoId);
        this.loadKeys(repoId);
      }
    });
  }

  /** Loads all metadata entries (global + repo) for the repository. @param repoId Repository ID. */
  load(repoId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<MetadataDto[]>(this.url(repoId)).subscribe({
      next:  items => { this.items.set(items); this.loading.set(false); },
      error: ()    => { this.error.set('Failed to load metadata'); this.loading.set(false); },
    });
  }

  /** Loads the well-known metadata keys with display titles. @param repoId Repository ID. */
  loadKeys(repoId: string): void {
    this.http.get<MetadataKeyOption[]>(`${this.url(repoId)}/keys`).subscribe({
      next: keys => this.keys.set(keys),
    });
  }

  /**
   * Creates a metadata entry.
   * @param repoId Repository ID.
   * @param payload Key, value, and global flag.
   * @returns Observable of the created MetadataDto.
   */
  create(repoId: string, payload: CreateMetadataPayload): Observable<MetadataDto> {
    return this.http.post<MetadataDto>(this.url(repoId), payload).pipe(
      tap(saved => this.items.update(list => [...list, saved])),
    );
  }

  /**
   * Updates a metadata entry's value. API responds 204; the local list is patched optimistically.
   * @param repoId Repository ID.
   * @param id Entry ID.
   * @param payload New value.
   * @returns Observable completing on success.
   */
  update(repoId: string, id: string, payload: UpdateMetadataPayload): Observable<void> {
    return this.http.put<void>(`${this.url(repoId)}/${id}`, payload).pipe(
      tap(() => this.items.update(list => list.map(m =>
        m.id === id ? { ...m, value: payload.value } : m))),
    );
  }

  /**
   * Soft-deletes a metadata entry.
   * @param repoId Repository ID.
   * @param id Entry ID.
   * @returns Observable completing on success.
   */
  delete(repoId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.url(repoId)}/${id}`).pipe(
      tap(() => this.items.update(list => list.filter(m => m.id !== id))),
    );
  }

  private url(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/metadata`;
  }
}
