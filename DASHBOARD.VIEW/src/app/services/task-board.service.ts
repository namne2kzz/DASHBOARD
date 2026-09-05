import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, forkJoin, of, switchMap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  DiscussionApiDto,
  HistoryApiDto,
  WorkItemDetailApiDto,
  WorkItemSummaryApiDto,
} from '../models/work-item-api.model';
import { PagedResult } from '../models/common.model';
import {
  DiscussionEntry,
  HistoryEntry,
  WorkItemPriority,
  WorkItemStatus,
  WorkItem,
  WorkItemType,
} from '../models/work-item.model';
import { PRIORITY_LABELS, STATUS_LABELS } from '../core/constants/system.constant';
import { ToastService } from '../core/components/toast/toast.service';
import { AuthService } from './auth.service';
import { RepositoryContextService } from './repository-context.service';
import { ResourceService } from './resource.service';
import {
  priorityFromApi,
  priorityToApi,
  resolveNewState,
  stateToStatus,
  statusToState,
  workItemTypeFromApi,
  workItemTypeToApi,
} from '../utils/work-item-mapper.util';

const API_BASE = `${environment.apiBaseUrl}/repositories`;

function summaryToWorkItem(dto: WorkItemSummaryApiDto): WorkItem {
  return {
    id: dto.id,
    workItemNumber: dto.workItemNumber,
    customId: dto.customId,
    title: dto.title,
    description: '',
    priority: priorityFromApi(dto.priority),
    status: stateToStatus(dto.state),
    state: dto.state,
    workItemType: workItemTypeFromApi(dto.workItemType),
    assignedToId: dto.assignedToId,
    sprint: dto.sprint,
    implementInBuild: null,
    fixedInVersion: null,
    acceptanceCriteria: null,
    storyPoints: null,
    stepsToReproduce: null,
    environment: null,
    rootCause: null,
    solution: null,
    impaction: null,
    unitTest: null,
    designReview: null,
    originalEstimate: null,
    remainingWork: null,
    completedWork: null,
    automated: null,
    testSteps: null,
    discussions: [],
    history: [],
  };
}

function detailToWorkItem(dto: WorkItemDetailApiDto): WorkItem {
  return {
    id: dto.id,
    workItemNumber: dto.workItemNumber,
    customId: dto.customId,
    title: dto.title,
    description: dto.description ?? '',
    priority: priorityFromApi(dto.priority),
    status: stateToStatus(dto.state),
    state: dto.state,
    workItemType: workItemTypeFromApi(dto.workItemType),
    assignedToId: dto.assignedToId,
    sprint: dto.sprint,
    implementInBuild: dto.implementInBuild ?? null,
    fixedInVersion: dto.fixedInVersion ?? null,
    acceptanceCriteria: dto.acceptanceCriteria ?? null,
    storyPoints: dto.storyPoints ?? null,
    stepsToReproduce: dto.stepsToReproduce ?? null,
    environment: dto.environment ?? null,
    rootCause: dto.rootCause ?? null,
    solution: dto.solution ?? null,
    impaction: dto.impaction ?? null,
    unitTest: dto.unitTest ?? null,
    designReview: dto.designReview ?? null,
    originalEstimate: dto.originalEstimate ?? null,
    remainingWork: dto.remainingWork ?? null,
    completedWork: dto.completedWork ?? null,
    automated: dto.automated ?? null,
    testSteps: dto.testSteps ?? null,
    discussions: [],
    history: [],
  };
}

function discussionDtoToEntry(dto: DiscussionApiDto): DiscussionEntry {
  return {
    id: dto.id,
    authorId: dto.authorId,
    authorName: dto.authorName,
    body: dto.body,
    createdAt: dto.createdAt,
  };
}

function historyDtoToEntry(dto: HistoryApiDto): HistoryEntry {
  return {
    id: dto.id,
    authorId: dto.authorId,
    authorName: dto.authorName,
    authorAvatar: dto.authorAvatar,
    message: dto.message,
    createdAt: dto.createdAt,
  };
}

@Injectable({ providedIn: 'root' })
export class TaskBoardService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly auth    = inject(AuthService);
  private readonly toast   = inject(ToastService);
  private readonly rs      = inject(ResourceService);

  readonly searchQuery = signal('');
  readonly priorityFilter = signal<'all' | WorkItemPriority>('all');
  readonly workItemTypeFilter = signal<'all' | WorkItemType>('all');
  readonly assignedToMeOnly = signal(false);

  readonly dialogMode = signal<'create' | 'edit' | null>(null);
  readonly dialogTaskId = signal<string | null>(null);
  readonly dialogLoading = signal(false);
  /** Pre-selected type when opening the create form via the type picker. */
  readonly createInitialType = signal<WorkItemType>('task');

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Discussions for the currently-open work item, loaded from the API when the dialog opens. */
  readonly discussions = signal<DiscussionEntry[]>([]);

  /** Audit-trail history for the currently-open work item, loaded from the API when the dialog opens. */
  readonly history = signal<HistoryEntry[]>([]);

  private readonly allTasks = signal<WorkItem[]>([]);

  readonly todoColumn = signal<WorkItem[]>([]);
  readonly inProgressColumn = signal<WorkItem[]>([]);
  readonly doneColumn = signal<WorkItem[]>([]);

  readonly workItems = computed(() => this.allTasks());

  /** Filtered view of all tasks based on search, priority, type, and assignee filters. */
  readonly filteredTasks = computed(() => {
    const tasks = this.allTasks();
    const q = this.searchQuery().trim().toLowerCase();
    const p = this.priorityFilter();
    const wt = this.workItemTypeFilter();
    const meOnly = this.assignedToMeOnly();
    const meId = this.auth.currentUser()?.userId ?? null;

    return tasks.filter(t => {
      if (meOnly && t.assignedToId !== meId) return false;
      const matchQ =
        !q ||
        t.title.toLowerCase().includes(q) ||
        t.id.toLowerCase().includes(q);
      const matchP = p === 'all' || t.priority === p;
      const matchWt = wt === 'all' || t.workItemType === wt;
      return matchQ && matchP && matchWt;
    });
  });

  readonly todoCount = computed(() => this.countByStatus('todo'));
  readonly inProgressCount = computed(() => this.countByStatus('in-progress'));
  readonly doneCount = computed(() => this.countByStatus('done'));

  readonly hasActiveFilters = computed(
    () =>
      this.searchQuery().trim().length > 0 ||
      this.priorityFilter() !== 'all' ||
      this.workItemTypeFilter() !== 'all' ||
      this.assignedToMeOnly(),
  );

  readonly dialogTask = computed(() => {
    const id = this.dialogTaskId();
    if (!id) return null;
    return this.allTasks().find(t => t.id === id) ?? null;
  });

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (repoId) this.loadWorkItems(repoId);
    });

    effect(
      () => {
        const filtered = this.filteredTasks();
        this.todoColumn.set(filtered.filter(t => t.status === 'todo'));
        this.inProgressColumn.set(filtered.filter(t => t.status === 'in-progress'));
        this.doneColumn.set(filtered.filter(t => t.status === 'done'));
      },
      { allowSignalWrites: true },
    );
  }

  /** @returns Priority display label. @param priority Priority key. */
  priorityLabel(priority: WorkItemPriority): string {
    return PRIORITY_LABELS[priority];
  }

  /** @returns Status display label for a column. @param status Board column key. */
  statusLabel(status: WorkItemStatus): string {
    return STATUS_LABELS[status];
  }

  columnSignal(status: WorkItemStatus) {
    switch (status) {
      case 'todo':        return this.todoColumn;
      case 'in-progress': return this.inProgressColumn;
      case 'done':        return this.doneColumn;
    }
  }

  /** Opens the create-new-item dialog with a pre-selected work item type. @param type Initial work item type (default: task). */
  openCreate(type: WorkItemType = 'task'): void {
    this.createInitialType.set(type);
    this.discussions.set([]);
    this.history.set([]);
    this.dialogTaskId.set(null);
    this.dialogMode.set('create');
  }

  /**
   * Loads the full work item detail and its discussions, then opens the edit dialog.
   * The dialog opens only after the detail is fetched to avoid partial form state.
   * @param task The summary-level work item to edit.
   */
  openEdit(task: WorkItem): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || this.dialogLoading()) return;

    this.dialogLoading.set(true);

    forkJoin({
      detail: this.http.get<WorkItemDetailApiDto>(`${API_BASE}/${repoId}/workitems/${task.id}`),
      discussions: this.http
        .get<DiscussionApiDto[]>(`${API_BASE}/${repoId}/workitems/${task.id}/discussions`)
        .pipe(catchError(() => of<DiscussionApiDto[]>([]))),
      history: this.http
        .get<HistoryApiDto[]>(`${API_BASE}/${repoId}/workitems/${task.id}/history`)
        .pipe(catchError(() => of<HistoryApiDto[]>([]))),
    }).subscribe({
      next: ({ detail, discussions, history }) => {
        this.allTasks.update(tasks =>
          tasks.map(t => (t.id === task.id ? detailToWorkItem(detail) : t)),
        );
        this.discussions.set(discussions.map(discussionDtoToEntry));
        this.history.set(history.map(historyDtoToEntry));
        this.dialogTaskId.set(task.id);
        this.dialogMode.set('edit');
        this.dialogLoading.set(false);
      },
      error: () => this.dialogLoading.set(false),
    });
  }

  /** Closes the current dialog. */
  closeDialog(): void {
    this.dialogMode.set(null);
    this.dialogTaskId.set(null);
  }

  /** Toggles the "assigned to me" filter. */
  toggleAssignedToMe(): void {
    this.assignedToMeOnly.update(v => !v);
  }

  /**
   * Posts a new comment on the work item's discussion thread.
   * @param workItemId The work item ID.
   * @param body The comment text.
   */
  addDiscussion(workItemId: string, body: string): void {
    const trimmed = body.trim();
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || !trimmed) return;

    this.http
      .post<DiscussionApiDto>(
        `${API_BASE}/${repoId}/workitems/${workItemId}/discussions`,
        JSON.stringify(trimmed),
        { headers: { 'Content-Type': 'application/json' } },
      )
      .subscribe({
        next: dto =>
          this.discussions.update(d => [discussionDtoToEntry(dto), ...d]),
      });
  }

  /**
   * Creates or updates a work item via the API.
   * Pass `id` in the payload to update; omit it to create.
   */
  upsertTask(
    payload: Omit<WorkItem, 'id' | 'workItemNumber' | 'discussions' | 'history'> & { id?: string },
  ): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    if (payload.id) {
      this.updateWorkItem(repoId, payload as WorkItem);
    } else {
      this.createWorkItem(repoId, payload);
    }
  }

  /**
   * Soft-deletes a work item via the API and removes it from the local list.
   * @param id The work item ID to delete.
   */
  deleteTask(id: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    this.http.delete(`${API_BASE}/${repoId}/workitems/${id}`).subscribe({
      next: () => this.allTasks.update(tasks => tasks.filter(t => t.id !== id)),
    });
  }

  /**
   * Applies column changes from a drag-and-drop event.
   * Detects status changes and calls PATCH /state for each affected item.
   * @param todo Current To Do column items.
   * @param inProgress Current In Progress column items.
   * @param done Current Done column items.
   */
  persistFromColumns(todo: WorkItem[], inProgress: WorkItem[], done: WorkItem[]): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const prev = this.allTasks();
    const touched = new Set([...todo, ...inProgress, ...done].map(t => t.id));
    const untouched = prev.filter(t => !touched.has(t.id));

    const merged: WorkItem[] = [
      ...todo.map(t => ({ ...t, status: 'todo' as WorkItemStatus })),
      ...inProgress.map(t => ({ ...t, status: 'in-progress' as WorkItemStatus })),
      ...done.map(t => ({ ...t, status: 'done' as WorkItemStatus })),
      ...untouched,
    ];

    this.allTasks.set(merged);

    for (const item of merged) {
      const old = prev.find(p => p.id === item.id);
      if (!old || old.status === item.status) continue;

      const newState = resolveNewState(old.status, item.status, item.workItemType);
      this.allTasks.update(tasks =>
        tasks.map(t => (t.id === item.id ? { ...t, state: newState } : t)),
      );
      this.http
        .patch(`${API_BASE}/${repoId}/workitems/${item.id}/state`, { newState })
        .subscribe({
          error: () =>
            this.allTasks.update(tasks =>
              tasks.map(t => (t.id === item.id ? { ...t, status: old.status, state: old.state } : t)),
            ),
        });
    }
  }

  /** Called by the repos service when a GitHub PR is merged — transitions item to Done via API. @param workItemId Work item ID. @param reason Reason message. */
  transitionWorkItemFromGitHub(workItemId: string, reason: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const item = this.allTasks().find(t => t.id === workItemId);
    if (!item || item.status === 'done') return;

    const newState = statusToState('done', workItemTypeToApi(item.workItemType));
    this.http
      .patch(`${API_BASE}/${repoId}/workitems/${workItemId}/state`, { newState })
      .subscribe({
        next: () =>
          this.allTasks.update(tasks =>
            tasks.map(t => (t.id === workItemId ? { ...t, status: 'done' } : t)),
          ),
      });

    void reason;
  }

  private loadWorkItems(repoId: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.http
      .get<PagedResult<WorkItemSummaryApiDto>>(
        `${API_BASE}/${repoId}/workitems?pageSize=100`,
      )
      .subscribe({
        next: result => {
          this.allTasks.set(result.items.map(summaryToWorkItem));
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load work items');
          this.loading.set(false);
        },
      });
  }

  private createWorkItem(
    repoId: string,
    payload: Omit<WorkItem, 'id' | 'workItemNumber' | 'discussions' | 'history'>,
  ): void {
    const body = {
      workItemType: workItemTypeToApi(payload.workItemType),
      title: payload.title,
      description: payload.description,
      priority: priorityToApi(payload.priority),
      assignedToId: payload.assignedToId ?? null,
      sprint: payload.sprint,
      implementInBuild: payload.implementInBuild || null,
      fixedInVersion: payload.fixedInVersion || null,
      acceptanceCriteria: payload.acceptanceCriteria || null,
      storyPoints: payload.storyPoints ?? null,
      stepsToReproduce: payload.stepsToReproduce || null,
      environment: payload.environment || null,
      rootCause: payload.rootCause || null,
      solution: payload.solution || null,
      impaction: payload.impaction || null,
      unitTest: payload.unitTest || null,
      designReview: payload.designReview || null,
      originalEstimate: payload.originalEstimate ?? null,
      remainingWork: payload.remainingWork ?? null,
      automated: payload.automated ?? null,
      testSteps: payload.testSteps?.length ? payload.testSteps : null,
    };

    this.http.post<WorkItemDetailApiDto>(`${API_BASE}/${repoId}/workitems`, body).subscribe({
      next: detail => {
        const newItem = detailToWorkItem(detail);
        this.allTasks.update(tasks => [...tasks, newItem]);

        if (payload.state !== newItem.state) {
          this.http
            .patch(`${API_BASE}/${repoId}/workitems/${newItem.id}/state`, { newState: payload.state })
            .subscribe({
              next: () =>
                this.allTasks.update(tasks =>
                  tasks.map(t =>
                    t.id === newItem.id
                      ? { ...t, state: payload.state, status: payload.status }
                      : t,
                  ),
                ),
            });
        }
      },
    });
  }

  private updateWorkItem(repoId: string, payload: WorkItem): void {
    const old = this.allTasks().find(t => t.id === payload.id);

    const body = {
      title: payload.title,
      description: payload.description,
      priority: priorityToApi(payload.priority),
      assignedToId: payload.assignedToId ?? null,
      sprint: payload.sprint,
      implementInBuild: payload.implementInBuild || null,
      fixedInVersion: payload.fixedInVersion || null,
      acceptanceCriteria: payload.acceptanceCriteria || null,
      storyPoints: payload.storyPoints ?? null,
      stepsToReproduce: payload.stepsToReproduce || null,
      environment: payload.environment || null,
      rootCause: payload.rootCause || null,
      solution: payload.solution || null,
      impaction: payload.impaction || null,
      unitTest: payload.unitTest || null,
      designReview: payload.designReview || null,
      originalEstimate: payload.originalEstimate ?? null,
      remainingWork: payload.remainingWork ?? null,
      automated: payload.automated ?? null,
      testSteps: payload.testSteps ?? null,
    };

    const stateChanged = old && old.state !== payload.state;

    this.http
      .put(`${API_BASE}/${repoId}/workitems/${payload.id}`, body)
      .pipe(
        // If state changed: PATCH state first, then re-fetch. Otherwise re-fetch directly.
        switchMap(() =>
          stateChanged
            ? this.http
                .patch(`${API_BASE}/${repoId}/workitems/${payload.id}/state`, { newState: payload.state })
                .pipe(switchMap(() => this.http.get<WorkItemDetailApiDto>(`${API_BASE}/${repoId}/workitems/${payload.id}`)))
            : this.http.get<WorkItemDetailApiDto>(`${API_BASE}/${repoId}/workitems/${payload.id}`),
        ),
      )
      .subscribe({
        next: detail => {
          this.allTasks.update(tasks =>
            tasks.map(t => (t.id === payload.id ? detailToWorkItem(detail as WorkItemDetailApiDto) : t)),
          );
        },
        error: () => this.toast.error(this.rs.get('toast.error.save')),
      });
  }

  private countByStatus(status: WorkItemStatus): number {
    return this.filteredTasks().filter(t => t.status === status).length;
  }
}
