import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PrivilegeService } from '../core/services/privilege.service';
import { RepositoryContextService } from './repository-context.service';
import { environment } from '../../environments/environment';
import { WorkflowColumnApiDto } from '../models/workflow-api.model';
import type { BoardColumn, SprintTaskStateKey, WipMode } from '../models/workflow.model';

export type { BoardColumn, SprintTaskStateKey, WipMode } from '../models/workflow.model';

// ── Mapping helpers ───────────────────────────────────────────────────────────

/** Maps SprintTaskStateKey → numeric API value (WorkItemState). */
const STATE_KEY_TO_INT: Record<SprintTaskStateKey, number> = {
  'open':        0,
  'todo':        1,
  'in-progress': 2,
  'in-review':   3,
  'verified':    4,
  'running':     5,
  'done':        6,
  'passed':      7,
  'failed':      8,
  'closed':      9,
};

/** Maps numeric WorkItemState int → SprintTaskStateKey. */
const INT_TO_STATE_KEY: Record<number, SprintTaskStateKey> = {
  0: 'open',
  1: 'todo',
  2: 'in-progress',
  3: 'in-review',
  4: 'verified',
  5: 'running',
  6: 'done',
  7: 'passed',
  8: 'failed',
  9: 'closed',
};

function stateToApi(state: SprintTaskStateKey): string {
  return (STATE_KEY_TO_INT[state] ?? 0).toString();
}

function mapColumn(dto: WorkflowColumnApiDto): BoardColumn {
  return {
    id:             dto.id,
    name:           dto.name,
    mappedState:    INT_TO_STATE_KEY[dto.mappedState] ?? 'open',
    wipLimit:       dto.wipLimit,
    wipMode:        dto.wipMode === 1 ? 'hard' : 'soft',
    agingLimitDays: dto.agingLimitDays,
  };
}

function wipModeToApi(mode: WipMode): string { return mode === 'hard' ? '1' : '0'; }

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx   = inject(RepositoryContextService);
  private readonly privilege  = inject(PrivilegeService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _columns = signal<BoardColumn[]>([]);
  private readonly _loading = signal(false);

  readonly canManageBoard = computed(() => this.privilege.canManageBoard());
  readonly columns        = this._columns.asReadonly();
  readonly loading        = this._loading.asReadonly();

  /** Columns that have a WIP limit configured (> 0). */
  readonly wipColumns = computed(() => this._columns().filter(c => c.wipLimit > 0));

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (!repoId) { this._columns.set([]); return; }
      this.loadBoard(repoId);
    });
  }

  /** Updates the WIP limit for a column. @param columnId Target column. @param value New limit (string from input). */
  updateWipLimit(columnId: string, value: string): void {
    this.patchColumn(columnId, { wipLimit: Math.max(0, Number(value) || 0) });
  }

  /** Sets WIP enforcement mode. @param columnId Target column. @param mode 'soft' warns; 'hard' blocks. */
  setWipMode(columnId: string, mode: WipMode): void {
    this.patchColumn(columnId, { wipMode: mode });
  }

  /** Updates the aging limit for a column. @param columnId Target column. @param value New limit in days (string from input). */
  updateAgingLimit(columnId: string, value: string): void {
    this.patchColumn(columnId, { agingLimitDays: Math.max(1, Number(value) || 1) });
  }

  /** Updates column name and state mapping inline. @param columnId Target column. @param name New display name. @param mappedState New sprint-task state. */
  renameAndRemap(columnId: string, name: string, mappedState: SprintTaskStateKey): void {
    this.patchColumn(columnId, { name, mappedState });
  }

  /** Creates a new column at the end of the board. @param name Display name. @param mappedState Sprint-task state. @param wipLimit Max items (0 = unlimited). @param wipMode Soft or Hard enforcement. @param agingLimitDays Days before aging flag. */
  createColumn(
    name: string,
    mappedState: SprintTaskStateKey,
    wipLimit: number,
    wipMode: WipMode,
    agingLimitDays: number,
  ): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const params = new HttpParams({
      fromObject: {
        name,
        mappedState:    stateToApi(mappedState),
        wipLimit:       wipLimit.toString(),
        wipMode:        wipModeToApi(wipMode),
        agingLimitDays: agingLimitDays.toString(),
      },
    });

    this.http
      .post<WorkflowColumnApiDto>(`${this.boardUrl(repoId)}/columns`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  dto => this._columns.update(cols => [...cols, mapColumn(dto)]),
        error: ()  => {},
      });
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private loadBoard(repoId: string): void {
    this._loading.set(true);
    this.http
      .get<WorkflowColumnApiDto[]>(`${this.boardUrl(repoId)}/columns`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  cols => { this._columns.set(cols.map(mapColumn)); this._loading.set(false); },
        error: ()   => this._loading.set(false),
      });
  }

  private patchColumn(columnId: string, patch: Partial<BoardColumn>): void {
    const repoId = this.repoCtx.selectedRepoId();
    const col    = this._columns().find(c => c.id === columnId);
    if (!repoId || !col) return;

    const updated = { ...col, ...patch };
    this._columns.update(cols => cols.map(c => (c.id === columnId ? updated : c)));

    const params = new HttpParams({
      fromObject: {
        name:           updated.name,
        mappedState:    stateToApi(updated.mappedState),
        wipLimit:       updated.wipLimit.toString(),
        wipMode:        wipModeToApi(updated.wipMode),
        agingLimitDays: updated.agingLimitDays.toString(),
      },
    });

    this.http
      .put(`${this.boardUrl(repoId)}/columns/${columnId}`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: () => this.loadBoard(repoId) });
  }

  private boardUrl(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/board`;
  }
}
