import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { SprintSelectionService } from './sprint-selection.service';
import { AuthService } from './auth.service';
import { BoardTaskApiDto, CapacityMemberApiDto, CreateWorkItemPayload, WorkItemPickerApiDto } from '../models/sprint-planning-api.model';
import { SprintTaskApiState, SprintTaskApiType } from '../core/enums/system.enum';
import type { BoardItem, BoardItemType, BoardPriority } from '../models/boards.model';
import type { SprintTaskStateKey } from '../models/workflow.model';

const API = `${environment.apiBaseUrl}/repositories`;

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

  readonly searchQuery      = signal('');
  readonly assignedToMeOnly = signal(false);

  private readonly _allItems        = signal<BoardItem[]>([]);
  private readonly _capacityMembers = signal<CapacityMemberApiDto[]>([]);

  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  readonly sprints = this.sprintSelection.sprints;
  /** Members with capacity defined on the active sprint — source for the "New work item" assignee picker. */
  readonly capacityMembers = this._capacityMembers.asReadonly();

  readonly selectedSprintId = this.sprintSelection.selectedSprintId;
  readonly selectedSprint   = this.sprintSelection.selectedSprint;

  /** Flat task list filtered by search text and "assigned to me". */
  readonly filteredItems = computed(() => {
    const items  = this._allItems();
    const q      = this.searchQuery().trim().toLowerCase();
    const meOnly = this.assignedToMeOnly();
    const meId   = this.auth.currentUser()?.userId ?? null;
    return items.filter(i => {
      if (meOnly && i.assignedToId !== meId) return false;
      return !q || i.title.toLowerCase().includes(q) || `${i.workItemNumber}`.includes(q);
    });
  });

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
        error: () => this.error.set('Failed to create work item'),
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
