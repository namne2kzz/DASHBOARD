import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { MemberApiDto } from '../models/member.model';
import { RepositoryApiDto, UpdateRepositoryPayload } from '../models/repository.model';
import { describeLoadError } from '../utils/http-error.util';

/**
 * Backs the Settings → Repositories screen.
 *
 * Kept apart from {@link RepositoryContextService}, which holds the repositories the signed-in
 * user can work in and drives the sidebar. This one lists every repository in the organization
 * including archived ones, so mixing the two would leak archived entries into the repo switcher.
 */
@Injectable({ providedIn: 'root' })
export class RepositoryAdminService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/repositories`;

  readonly repositories = signal<RepositoryApiDto[]>([]);
  readonly loading      = signal(false);
  readonly error        = signal<string | null>(null);

  /**
   * Loads every repository in the organization, archived ones included.
   * @param search Optional name or code filter.
   */
  load(search = ''): void {
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams().set('includeArchived', 'true');
    if (search.trim()) params = params.set('search', search.trim());

    this.http.get<RepositoryApiDto[]>(this.baseUrl, { params }).subscribe({
      next:  repos => { this.repositories.set(repos ?? []); this.loading.set(false); },
      error: (err: unknown) => {
        this.error.set(describeLoadError(err, 'Failed to load repositories'));
        this.loading.set(false);
      },
    });
  }

  /**
   * Updates a repository's name, description and optionally its Project Manager.
   * @param repoId Repository to update.
   * @param payload New values; omit `leadId` to leave the role where it is.
   * @returns Observable completing on success.
   */
  update(repoId: string, payload: UpdateRepositoryPayload): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${repoId}`, payload).pipe(
      // Patched locally rather than reloading: the list may be filtered or scrolled, and a full
      // refresh would throw away that position for one row's worth of change.
      tap(() => this.patch(repoId, {
        name:        payload.name,
        description: payload.description,
      })),
    );
  }

  /**
   * Archives a repository, hiding it from ordinary views without deleting anything.
   * @param repoId Repository to archive.
   * @returns Observable completing on success.
   */
  archive(repoId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${repoId}`).pipe(
      tap(() => this.patch(repoId, { isArchived: true })),
    );
  }

  /**
   * Restores an archived repository.
   * @param repoId Repository to restore.
   * @returns Observable completing on success.
   */
  unarchive(repoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${repoId}/unarchive`, {}).pipe(
      tap(() => this.patch(repoId, { isArchived: false })),
    );
  }

  /**
   * Fetches one repository's members, to populate the Project Manager picker while editing it.
   *
   * Read straight through rather than via {@link MembersService}, whose single members signal
   * tracks the repository the user is working in — a dialog editing some other repository would
   * otherwise overwrite it.
   * @param repoId Repository whose members to list.
   * @returns Observable of that repository's members.
   */
  members(repoId: string): Observable<MemberApiDto[]> {
    return this.http.get<MemberApiDto[]>(`${this.baseUrl}/${repoId}/members`);
  }

  /** Applies a partial change to one row in the local list. */
  private patch(repoId: string, changes: Partial<RepositoryApiDto>): void {
    this.repositories.update(list =>
      list.map(r => r.id === repoId ? { ...r, ...changes } : r));
  }
}
