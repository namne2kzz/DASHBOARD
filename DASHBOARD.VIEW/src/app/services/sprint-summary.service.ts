import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, Subject, switchMap } from 'rxjs';
import { SprintApiDto, SprintSummaryApiDto } from '../models/sprint-api.model';
import { RepositoryContextService } from './repository-context.service';

type SprintStatus = 'active' | 'upcoming' | 'completed';

@Injectable({ providedIn: 'root' })
export class SprintSummaryService {
  private readonly http = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly loadTrigger$ = new Subject<string | null>();

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeSprint = signal<SprintApiDto | null>(null);
  readonly summary = signal<SprintSummaryApiDto | null>(null);

  readonly sprintStatus = computed<SprintStatus>(() => {
    const s = this.activeSprint();
    if (!s) return 'completed';
    if (s.isActive) return 'active';
    const today = this.todayIso();
    if (s.startDate > today) return 'upcoming';
    return 'completed';
  });

  /** Days until sprint end (negative = already ended). */
  readonly daysRemaining = computed<number | null>(() => {
    const s = this.activeSprint();
    if (!s) return null;
    const end = this.parseLocalDate(s.endDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.ceil((end.getTime() - today.getTime()) / 86_400_000);
  });

  /** 0–100 story-point completion percentage. */
  readonly storyPointPercent = computed<number>(() => {
    const sm = this.summary();
    if (!sm || sm.committedStoryPoints === 0) return 0;
    return Math.min(100, Math.round((sm.completedStoryPoints / sm.committedStoryPoints) * 100));
  });

  /** 0–100 task completion percentage. */
  readonly taskPercent = computed<number>(() => {
    const sm = this.summary();
    if (!sm || sm.totalTasks === 0) return 0;
    return Math.min(100, Math.round((sm.completedTasks / sm.totalTasks) * 100));
  });

  constructor() {
    this.loadTrigger$
      .pipe(
        switchMap(repoId => {
          if (!repoId) {
            this.activeSprint.set(null);
            this.summary.set(null);
            this.loading.set(false);
            return of(null);
          }
          return this.http.get<SprintApiDto[]>(`/api/repositories/${repoId}/sprints`).pipe(
            switchMap(sprints => {
              const active = sprints.find(s => s.isActive) ?? null;
              this.activeSprint.set(active);
              if (!active) {
                this.summary.set(null);
                return of(null);
              }
              return this.http.get<SprintSummaryApiDto>(
                `/api/repositories/${repoId}/sprints/${active.id}/summary`,
              );
            }),
            catchError((err: HttpErrorResponse) => {
              console.error('[SprintSummaryService] HTTP', err.status, err.url, err.error);
              this.activeSprint.set(null);
              this.summary.set(null);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(result => {
        if (result !== null) this.summary.set(result);
        this.loading.set(false);
      });

    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      this.loading.set(true);
      this.error.set(null);
      this.loadTrigger$.next(repoId);
    });
  }

  /** Formats an ISO date-only string to "May 4" without timezone shift. @param iso Date string "YYYY-MM-DD". @returns Short month + day. */
  formatDate(iso: string): string {
    const d = this.parseLocalDate(iso);
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  private parseLocalDate(iso: string): Date {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  private todayIso(): string {
    return new Date().toISOString().split('T')[0];
  }
}
