import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, Subject, switchMap } from 'rxjs';
import { environment } from '../../environments/environment';
import { OverviewStatsDto } from '../models/overview.model';
import { RepositoryContextService } from './repository-context.service';

@Injectable({ providedIn: 'root' })
export class OverviewService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  private readonly loadTrigger$ = new Subject<{ repoId: string | null; sprintId: string | null }>();

  readonly loading        = signal(false);
  readonly error          = signal<string | null>(null);
  readonly stats          = signal<OverviewStatsDto | null>(null);
  readonly selectedSprintId = signal<string | null>(null);

  constructor() {
    this.loadTrigger$
      .pipe(
        switchMap(({ repoId, sprintId }) => {
          if (!repoId) {
            this.stats.set(null);
            this.loading.set(false);
            return of(null);
          }
          const base = `${environment.apiBaseUrl}/repositories/${repoId}/overview`;
          const url  = sprintId ? `${base}?sprintId=${sprintId}` : base;
          return this.http.get<OverviewStatsDto>(url).pipe(
            catchError((err: HttpErrorResponse) => {
              this.error.set(err.message);
              this.stats.set(null);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(result => {
        if (result !== null) this.stats.set(result);
        this.loading.set(false);
      });

    effect(() => {
      const repoId   = this.repoCtx.selectedRepoId();
      const sprintId = this.selectedSprintId();
      this.loading.set(true);
      this.error.set(null);
      this.loadTrigger$.next({ repoId, sprintId });
    });
  }

  /** Selects a sprint and reloads overview statistics. @param sprintId Sprint ID, or null for the default sprint. */
  selectSprint(sprintId: string | null): void {
    this.selectedSprintId.set(sprintId);
  }
}
