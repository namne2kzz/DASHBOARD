import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { SprintSelectionService } from './sprint-selection.service';
import { AuthService } from './auth.service';
import { ToastService } from '../core/components/toast/toast.service';
import { BoardTaskApiDto, CapacityMemberApiDto, CreateWorkItemPayload, WorkItemPickerApiDto } from '../models/sprint-planning-api.model';
import { SprintTaskApiState, SprintTaskApiType } from '../core/enums/system.enum';
import { SPRINT_TASK_STATE_LABEL } from '../core/constants/system.constant';
import type { BoardItem, BoardItemType, BoardPriority } from '../models/boards.model';
import type { SprintTaskStateKey } from '../models/workflow.model';

const API = `${environment.apiBaseUrl}/repositories`;

/** One row of the board's dynamic query filter. */
export interface QueryRow {
  logicalOperator: 'and' | 'or';
  criteria: string;
  operation: string;
  value: string;
}

const QUERY_TYPE_LABEL: Record<BoardItemType, string> = {
  'user-story': 'User Story',
  'task':       'Task',
  'bug':        'Bug',
  'test-plan':  'Test Plan',
};

/** Resolves the comparable text of a board item field for the dynamic query. */
function queryFieldText(item: BoardItem, criteria: string): string {
  switch (criteria) {
    case 'title':      return item.title;
    case 'type':       return QUERY_TYPE_LABEL[item.type];
    case 'priority':   return item.priority;
    case 'state':      return SPRINT_TASK_STATE_LABEL[item.apiState];
    case 'assignedTo': return item.assignedToName ?? '';
    case 'remaining':  return String(item.remainingWork);
    case 'labels':     return item.labels.join(' ');
    default:           return '';
  }
}

/** Evaluates a single query row against an item. Labels are matched per-value (any-of). */
function matchQueryRow(item: BoardItem, row: QueryRow): boolean {
  const val = row.value.trim().toLowerCase();

  if (row.criteria === 'labels') {
    const labels = item.labels.map(l => l.toLowerCase());
    switch (row.operation) {
      case 'equals':       return labels.includes(val);
      case 'not-equals':   return !labels.includes(val);
      case 'contains':     return labels.some(l => l.includes(val));
      case 'not-contains': return !labels.some(l => l.includes(val));
      case 'starts-with':  return labels.some(l => l.startsWith(val));
      case 'is-empty':     return labels.length === 0;
      case 'is-not-empty': return labels.length > 0;
      default:             return true;
    }
  }

  const text = queryFieldText(item, row.criteria).toLowerCase();
  switch (row.operation) {
    case 'equals':       return text === val;
    case 'not-equals':   return text !== val;
    case 'contains':     return text.includes(val);
    case 'not-contains': return !text.includes(val);
    case 'starts-with':  return text.startsWith(val);
    case 'is-empty':     return text.length === 0;
    case 'is-not-empty': return text.length > 0;
    default:             return true;
  }
}

/** A row participates only when it has a field and either a value or an emptiness operator. */
function isQueryRowActive(row: QueryRow): boolean {
  return !!row.criteria &&
    (row.value.trim().length > 0 || row.operation === 'is-empty' || row.operation === 'is-not-empty');
}

/** Combines all active rows with their per-row AND/OR operator (left-to-right). */
function matchQuery(item: BoardItem, rows: QueryRow[]): boolean {
  const active = rows.filter(isQueryRowActive);
  if (active.length === 0) return true;
  let result = matchQueryRow(item, active[0]);
  for (let k = 1; k < active.length; k++) {
    const m = matchQueryRow(item, active[k]);
    result = active[k].logicalOperator === 'or' ? result || m : result && m;
  }
  return result;
}

/** Maps SprintTaskStateKey string → SprintTaskApiState numeric value. */
const STATE_KEY_TO_API: Record<SprintTaskStateKey, SprintTaskApiState> = {
  'new':       SprintTaskApiState.New,
  'backlog':   SprintTaskApiState.Backlog,
  'todo':      SprintTaskApiState.Todo,
  'active':    SprintTaskApiState.Active,
  'in-review': SprintTaskApiState.InReview,
  'done':      SprintTaskApiState.Done,
};

const TYPE_MAP: Record<SprintTaskApiType, BoardItemType> = {
  [SprintTaskApiType.UserStory]: 'user-story',
  [SprintTaskApiType.Task]:      'task',
  [SprintTaskApiType.Bug]:       'bug',
  [SprintTaskApiType.TestPlan]:  'test-plan',
};

export const PRIORITY_MAP: BoardPriority[] = ['low', 'medium', 'high', 'critical'];

function toItem(dto: BoardTaskApiDto): BoardItem {
  return {
    id:               dto.id,
    workItemNumber:   dto.workItemNumber,
    type:             TYPE_MAP[dto.type] ?? 'task',
    title:            dto.title,
    description:      '',
    priority:         PRIORITY_MAP[dto.priority] ?? 'medium',
    columnState:      'todo',
    apiState:         dto.state,
    apiType:          dto.type,
    assignedToId:     dto.assignedToId,
    assignedToName:   dto.assignedToName,
    assignedToAvatar: dto.assignedToAvatar,
    stepsToReproduce: null,
    environment:      null,
    rootCause:        null,
    solution:         null,
    impaction:        null,
    unitTest:         null,
    designReview:     null,
    originalEstimate: dto.originalEstimate,
    remainingWork:    dto.remainingWork,
    completedWork:    dto.completedWork,
    testSteps:        null,
    automated:        null,
    discussions:      [],
    history:          [],
    stateChangedAt:   dto.stateChangedAt,
    labels:           dto.labels ?? [],
  };
}

export { STATE_KEY_TO_API };

@Injectable({ providedIn: 'root' })
export class SprintBoardService {
  private readonly http            = inject(HttpClient);
  private readonly repoCtx         = inject(RepositoryContextService);
  private readonly sprintSelection = inject(SprintSelectionService);
  private readonly destroyRef      = inject(DestroyRef);
  private readonly auth            = inject(AuthService);
  private readonly toast           = inject(ToastService);

  readonly searchQuery      = signal('');
  readonly assignedToMeOnly = signal(false);

  /** Dynamic query filter rows applied to the board (live). */
  readonly queryRows = signal<QueryRow[]>([{ logicalOperator: 'and', criteria: '', operation: 'equals', value: '' }]);

  private readonly _allItems        = signal<BoardItem[]>([]);
  private readonly _capacityMembers = signal<CapacityMemberApiDto[]>([]);

  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  readonly sprints = this.sprintSelection.sprints;
  /** Members with capacity defined on the active sprint — source for the "New work item" assignee picker. */
  readonly capacityMembers = this._capacityMembers.asReadonly();

  readonly selectedSprintId = this.sprintSelection.selectedSprintId;
  readonly selectedSprint   = this.sprintSelection.selectedSprint;

  /** Flat task list filtered by search text, "assigned to me", and the dynamic query. */
  readonly filteredItems = computed(() => {
    const items  = this._allItems();
    const q      = this.searchQuery().trim().toLowerCase();
    const meOnly = this.assignedToMeOnly();
    const meId   = this.auth.currentUser()?.userId ?? null;
    const rows   = this.queryRows();
    return items.filter(i => {
      if (meOnly && i.assignedToId !== meId) return false;
      if (q && !i.title.toLowerCase().includes(q) && !`${i.workItemNumber}`.includes(q)) return false;
      return matchQuery(i, rows);
    });
  });

  /** Resets the dynamic query to a single empty row. */
  clearQuery(): void {
    this.queryRows.set([{ logicalOperator: 'and', criteria: '', operation: 'equals', value: '' }]);
  }

  constructor() {
    // Load board tasks whenever the selected sprint changes.
    effect(() => {
      const repoId   = this.repoCtx.selectedRepoId();
      const sprintId = this.selectedSprintId();
      this._allItems.set([]);
      if (!repoId || !sprintId) return;
      this.loadTasks(repoId, sprintId);
    });

    // Load capacity members whenever the selected sprint changes.
    effect(() => {
      const repoId   = this.repoCtx.selectedRepoId();
      const sprintId = this.selectedSprintId();
      this._capacityMembers.set([]);
      if (!repoId || !sprintId) return;
      this.loadCapacityMembers(repoId, sprintId);
    });
  }

  /** Switches the active sprint. @param id Sprint ID to select. */
  selectSprint(id: string): void { this.sprintSelection.selectSprint(id); }

  /**
   * Merges a partial update into a board item already in the local list — keeps cards in sync
   * with edits made elsewhere (e.g. the task detail dialog) without a full reload.
   * @param itemId The board item to patch.
   * @param patch Fields to merge.
   */
  patchItem(itemId: string, patch: Partial<BoardItem>): void {
    this._allItems.update(items =>
      items.map(i => (i.id === itemId ? { ...i, ...patch } : i)),
    );
  }

  /** Toggles the "assigned to me" filter. */
  toggleAssignedToMe(): void { this.assignedToMeOnly.update(v => !v); }

  /**
   * Optimistically moves an item to the target column state, then syncs to the API.
   * Rolls back the local change if the PATCH request fails.
   * @param item The board item being moved.
   * @param targetState The mappedState key of the destination column.
   */
  moveItemToState(item: BoardItem, targetState: SprintTaskStateKey): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    const newApiState    = STATE_KEY_TO_API[targetState];
    const nowIso         = new Date().toISOString();
    this._allItems.update(items =>
      items.map(i => i.id === item.id ? { ...i, apiState: newApiState, stateChangedAt: nowIso } : i),
    );

    this.http
      .patch(`${API}/${repoId}/sprints/${sprintId}/tasks/${item.id}/state?newState=${newApiState}`, null)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: () => this._allItems.update(items =>
          items.map(i => i.id === item.id ? { ...i, apiState: item.apiState } : i),
        ),
      });
  }

  /**
   * Creates a new work item (Task, Bug, or TestPlan) in the active sprint and refreshes the board.
   * @param payload Type-specific create fields; null values are omitted from the request.
   */
  createWorkItem(payload: CreateWorkItemPayload): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    let params = new HttpParams()
      .set('type', payload.type.toString())
      .set('title', payload.title)
      .set('description', payload.description)
      .set('priority', payload.priority.toString())
      .set('originalEstimate', payload.originalEstimate.toString());

    if (payload.assignedToId)             params = params.set('assignedToId', payload.assignedToId);
    if (payload.parentId)                 params = params.set('parentId', payload.parentId);
    if (payload.stepsToReproduce)         params = params.set('stepsToReproduce', payload.stepsToReproduce);
    if (payload.environment)              params = params.set('environment', payload.environment);
    if (payload.rootCause)                params = params.set('rootCause', payload.rootCause);
    if (payload.solution)                 params = params.set('solution', payload.solution);
    if (payload.impaction)                params = params.set('impaction', payload.impaction);
    if (payload.unitTest)                 params = params.set('unitTest', payload.unitTest);
    if (payload.designReview)             params = params.set('designReview', payload.designReview);
    if (payload.automated !== null)       params = params.set('automated', String(payload.automated));
    for (const step of payload.testSteps ?? []) params = params.append('testSteps', step);

    this.http
      .post(`${API}/${repoId}/sprints/${sprintId}/tasks`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.loadTasks(repoId, sprintId),
        error: () => this.toast.error('Failed to create work item'),
      });
  }

  /**
   * Searches User Story items in the active repository for the create-work-item "Parent" picker.
   * @param q Title or work item number fragment to match.
   * @returns Observable of up to 20 matching items; empty if no repo is selected.
   */
  searchParentStories(q: string): Observable<WorkItemPickerApiDto[]> {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return of([]);
    const params = new HttpParams().set('q', q);
    return this.http.get<WorkItemPickerApiDto[]>(`${API}/${repoId}/items/picker`, { params });
  }

  private loadCapacityMembers(repoId: string, sprintId: string): void {
    this.http.get<{ members: CapacityMemberApiDto[] }>(`${API}/${repoId}/sprints/${sprintId}/capacity`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  res => this._capacityMembers.set(res.members),
        error: ()  => this._capacityMembers.set([]),
      });
  }

  private loadTasks(repoId: string, sprintId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<BoardTaskApiDto[]>(`${API}/${repoId}/sprints/${sprintId}/tasks/board`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  dtos => { this._allItems.set(dtos.map(toItem)); this.loading.set(false); },
        error: ()   => { this.error.set('Failed to load board tasks'); this.loading.set(false); },
      });
  }
}
