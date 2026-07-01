import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { SprintApiDto } from '../models/sprint-planning-api.model';

const STORAGE_KEY_PREFIX = 'selectedSprintId:';

/**
 * Single source of truth for "which sprint is selected" within the active repository.
 * Shared by the Board and Sprint Planning pages so the selection stays consistent and
 * survives navigation between them via localStorage.
 */
@Injectable({ providedIn: 'root' })
export class SprintSelectionService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx    = inject(RepositoryContextService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _sprints          = signal<SprintApiDto[]>([]);
  private readonly _selectedSprintId = signal<string | null>(null);
  private readonly _loading          = signal(false);
  private readonly _error            = signal<string | null>(null);

  readonly sprints = this._sprints.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error   = this._error.asReadonly();

  readonly selectedSprintId = this._selectedSprintId.asReadonly();

  readonly selectedSprint = computed(() =>
    this._sprints().find(s => s.id === this.selectedSprintId()) ?? null,
  );

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      this._sprints.set([]);
      this._selectedSprintId.set(null);
      this._error.set(null);
      if (!repoId) return;
      this.loadSprints(repoId);
    });
  }

  /**
   * Selects a sprint and persists the choice to localStorage so other pages restore it.
   * @param sprintId Sprint to select.
   */
  selectSprint(sprintId: string): void {
    this._selectedSprintId.set(sprintId);
    const repoId = this.repoCtx.selectedRepoId();
    if (repoId) localStorage.setItem(this.storageKey(repoId), sprintId);
  }

  /** Appends a newly created sprint to the list and selects it. @param sprint The created sprint. */
  addSprint(sprint: SprintApiDto): void {
    this._sprints.update(list => [...list, sprint]);
    this.selectSprint(sprint.id);
  }

  /** Merges a partial update into an existing sprint in the list. @param sprintId Sprint to update. @param patch Fields to merge. */
  patchSprint(sprintId: string, patch: Partial<SprintApiDto>): void {
    this._sprints.update(list => list.map(s => (s.id === sprintId ? { ...s, ...patch } : s)));
  }

  private loadSprints(repoId: string): void {
    this._loading.set(true);
    this.http.get<SprintApiDto[]>(`${environment.apiBaseUrl}/repositories/${repoId}/sprints`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: sprints => {
          this._sprints.set(sprints);
          this._loading.set(false);

          const savedId = localStorage.getItem(this.storageKey(repoId));
          const target   = (savedId ? sprints.find(s => s.id === savedId) : undefined)
            ?? this.pickDefaultSprint(sprints);
          if (target) this._selectedSprintId.set(target.id);
        },
        error: () => { this._error.set('Failed to load sprints'); this._loading.set(false); },
      });
  }

  /** Picks the most relevant sprint when nothing was previously selected: active, else nearest future, else most recent past. */
  private pickDefaultSprint(sprints: SprintApiDto[]): SprintApiDto | undefined {
    if (!sprints.length) return undefined;
    const today = new Date().toISOString().slice(0, 10);

    const active = sprints.find(s => s.startDate <= today && s.endDate >= today);
    if (active) return active;

    const future = sprints
      .filter(s => s.startDate > today)
      .sort((a, b) => a.startDate.localeCompare(b.startDate))[0];
    if (future) return future;

    return sprints
      .filter(s => s.endDate < today)
      .sort((a, b) => b.endDate.localeCompare(a.endDate))[0];
  }

  private storageKey(repoId: string): string {
    return `${STORAGE_KEY_PREFIX}${repoId}`;
  }
}
