import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable, Subject, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { CreateRepositoryPayload, RepositoryApiDto } from '../models/repository.model';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { TokenService } from '../core/services/token.service';

@Injectable({ providedIn: 'root' })
export class RepositoryContextService {
  private readonly http  = inject(HttpClient);
  private readonly token = inject(TokenService);
  private readonly baseUrl = `${environment.apiBaseUrl}/repositories`;

  private readonly loaded$ = new Subject<true>();

  /** Emits (without replay) each time a load cycle completes — guards use take(1) to wait for it. */
  readonly ready$: Observable<true> = this.loaded$.asObservable();

  readonly repositories  = signal<RepositoryApiDto[]>([]);
  readonly selectedRepoId = signal<string | null>(localStorage.getItem(this.repoKey()));
  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  readonly selectedRepo = computed(
    () => this.repositories().find(r => r.id === this.selectedRepoId()) ?? null,
  );

  constructor() {
    this.load();
  }

  /** @returns localStorage key scoped to the current user to prevent cross-user bleed. */
  private repoKey(): string {
    return `${StorageKeys.selectedRepoId}.${this.token.userId ?? 'anonymous'}`;
  }

  /**
   * Checks whether a repository code is available (not already taken).
   * @param code Candidate code to validate.
   * @returns Observable of true when available, false when already in use.
   */
  checkCode(code: string): Observable<boolean> {
    return this.http
      .get<{ available: boolean }>(`${this.baseUrl}/check-code`, { params: { code } })
      .pipe(map(r => r.available));
  }

  /**
   * Creates a new repository and appends it to the local list.
   * @param payload Name, code, description, and Scrum Master user ID.
   * @returns Observable of the created RepositoryApiDto.
   */
  create(payload: CreateRepositoryPayload): Observable<RepositoryApiDto> {
    return this.http.post<RepositoryApiDto>(this.baseUrl, payload).pipe(
      tap(repo => this.repositories.update(list => [...list, repo])),
    );
  }

  /** Loads the list of repositories accessible to the current user. */
  load(): void {
    this.repositories.set([]);
    // Re-read from the user-scoped key so a user switch mid-session picks up the correct value.
    this.selectedRepoId.set(localStorage.getItem(this.repoKey()));
    this.loading.set(true);
    this.error.set(null);
    this.http.get<RepositoryApiDto[]>(this.baseUrl).subscribe({
      next: repos => {
        this.repositories.set(repos);
        const stored     = this.selectedRepoId();
        const stillValid = stored !== null && repos.some(r => r.id === stored);
        if (!stillValid) {
          if (repos.length > 0) {
            this.select(repos[0].id);
          } else {
            this.selectedRepoId.set(null);
            localStorage.removeItem(this.repoKey());
          }
        }
        this.loading.set(false);
        this.loaded$.next(true);
      },
      error: () => {
        this.error.set('Failed to load repositories');
        this.loading.set(false);
        this.loaded$.next(true);
      },
    });
  }

  /** Sets the active repository and persists the selection under the current user's key. @param id Repository ID. */
  select(id: string): void {
    this.selectedRepoId.set(id);
    localStorage.setItem(this.repoKey(), id);
  }
}
