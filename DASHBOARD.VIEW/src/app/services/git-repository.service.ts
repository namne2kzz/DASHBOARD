import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  GitRepositoryConnectionSummary,
  GitRepositoryOverview,
} from '../models/git-repository.model';
import { RepositoryContextService } from './repository-context.service';

/**
 * Feature service for the Repos tab. Loads the list of configured GitHub connections and the
 * live overview (branches/commits/PRs/rate limit) for the currently selected project.
 *
 * Reactivity flow: an `effect()` watches `RepositoryContextService.selectedRepoId()` — the same
 * project-scoped pattern used by `MetadataService`/`OverviewService` — and reloads both the
 * connection list and the overview whenever the user switches projects. Connection tokens are
 * never part of this service's state; the backend never returns them.
 */
@Injectable({ providedIn: 'root' })
export class GitRepositoryService {
  private readonly http = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  readonly connections = signal<GitRepositoryConnectionSummary[]>([]);
  readonly overview = signal<GitRepositoryOverview | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Repo URL of the connection currently being viewed, or null to use the project's primary connection. */
  readonly selectedRepoUrl = signal<string | null>(null);

  readonly hasConnection = computed(() => this.overview()?.hasConnection ?? false);

  constructor() {
    effect(() => {
      const repositoryId = this.repoCtx.selectedRepoId();
      if (!repositoryId) {
        this.reset();
        return;
      }
      this.selectedRepoUrl.set(null);
      this.loadForRepository(repositoryId);
    });
  }

  /**
   * Loads the connection list and the overview (for the primary connection, or `repoUrl` if given) for a project.
   * @param repositoryId Project (Repository) ID whose GitHub connections should be loaded.
   * @param repoUrl Optional connection URL to view instead of the project's primary connection.
   */
  loadForRepository(repositoryId: string, repoUrl?: string): void {
    this.http.get<GitRepositoryConnectionSummary[]>(this.connectionsUrl(repositoryId)).subscribe({
      next: connections => this.connections.set(connections),
      error: () => this.connections.set([]),
    });
    this.loadOverview(repositoryId, repoUrl);
  }

  /**
   * Switches the overview to a different configured connection for the current project.
   * @param repositoryId Project (Repository) ID.
   * @param repoUrl Repo URL of the connection to view.
   */
  selectConnection(repositoryId: string, repoUrl: string): void {
    this.selectedRepoUrl.set(repoUrl);
    this.loadOverview(repositoryId, repoUrl);
  }

  /**
   * Reloads the overview for the current project and currently selected connection (manual refresh).
   * @param repositoryId Project (Repository) ID.
   */
  refresh(repositoryId: string): void {
    this.loadOverview(repositoryId, this.selectedRepoUrl() ?? undefined);
  }

  /** Clears all state — call on logout so no project's GitHub data lingers in memory for the next session. */
  reset(): void {
    this.connections.set([]);
    this.overview.set(null);
    this.selectedRepoUrl.set(null);
    this.loading.set(false);
    this.error.set(null);
  }

  /**
   * Fetches the overview snapshot for a project's connection.
   * @param repositoryId Project (Repository) ID.
   * @param repoUrl Optional connection URL; omitted uses the project's primary connection.
   */
  private loadOverview(repositoryId: string, repoUrl?: string): void {
    this.loading.set(true);
    this.error.set(null);
    const params = repoUrl ? new HttpParams().set('repoUrl', repoUrl) : undefined;
    this.http.get<GitRepositoryOverview>(this.overviewUrl(repositoryId), { params }).subscribe({
      next: overview => {
        this.overview.set(overview);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load repository overview');
        this.overview.set(null);
        this.loading.set(false);
      },
    });
  }

  private connectionsUrl(repositoryId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repositoryId}/git-repositories`;
  }

  private overviewUrl(repositoryId: string): string {
    return `${this.connectionsUrl(repositoryId)}/overview`;
  }
}
