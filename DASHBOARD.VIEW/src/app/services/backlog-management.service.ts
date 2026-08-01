import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, of, Subject, switchMap } from 'rxjs';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import {
  BacklogItemApiDto,
  CreateBacklogItemApiRequest,
  MoveToIterationApiRequest,
  RankBacklogItemApiRequest,
  SprintApiDto,
  UpdateBacklogItemApiRequest,
} from '../models/backlog-api.model';
import { BacklogItemApiState, BacklogItemApiType, BacklogTshirtSize } from '../core/enums/system.enum';
import { BacklogItem, BacklogLevel, BacklogState, EstimateMode, TshirtSize } from '../models/backlog.model';
import { BACKLOG_LEVELS, FIBONACCI_POINTS, TSHIRT_SIZES } from '../core/constants/system.constant';
import { RepositoryContextService } from './repository-context.service';
import { PrivilegeService } from '../core/services/privilege.service';
import { ToastService } from '../core/components/toast/toast.service';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BacklogManagementService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx    = inject(RepositoryContextService);
  private readonly privilege  = inject(PrivilegeService);
  private readonly toast      = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _loadTrigger$   = new Subject<string | null>();
  private readonly _activeLevel    = signal<BacklogLevel>('user-story');
  private readonly _estimateMode   = signal<EstimateMode>('fibonacci');
  private readonly _items          = signal<BacklogItem[]>([]);
  private readonly _sprints        = signal<SprintApiDto[]>([]);
  private readonly _selectedEpicId = signal<string | null>(null);
  private readonly _filterKeyword  = signal('');
  private readonly _filterState    = signal<BacklogState | ''>('');
  private readonly _filterSprintId = signal('');
  private readonly _loading        = signal(false);

  /** How many Product Backlog rows to render initially and per "load more" step. */
  private static readonly PAGE_SIZE = 25;
  private readonly _visibleCount = signal(BacklogManagementService.PAGE_SIZE);

  /** True when the current user can manage the backlog. */
  readonly canManageBacklog  = computed(() => this.privilege.canManageBacklog());
  /** True when the current user can create work items. */
  readonly canCreateWorkItem = computed(() => this.privilege.canCreateWorkItem());
  /** True when the current user can edit work items. */
  readonly canEditWorkItem   = computed(() => this.privilege.canEditWorkItem());

  readonly loading        = this._loading.asReadonly();
  readonly sprints        = this._sprints.asReadonly();
  readonly selectedEpicId = this._selectedEpicId.asReadonly();
  readonly filterKeyword  = this._filterKeyword.asReadonly();
  readonly filterState    = this._filterState.asReadonly();
  readonly filterSprintId = this._filterSprintId.asReadonly();
  readonly levels = BACKLOG_LEVELS;
  readonly fibonacciPoints         = FIBONACCI_POINTS;
  readonly tshirtSizes             = TSHIRT_SIZES;
  readonly activeLevel             = this._activeLevel.asReadonly();
  readonly estimateMode            = this._estimateMode.asReadonly();
  readonly items                   = this._items.asReadonly();

  readonly visibleItems = computed(() => {
    const level   = this._activeLevel();
    const epicId  = this._selectedEpicId();
    const items   = this._items();

    let filtered = items.filter(i => i.type === level);

    if (epicId) {
      if (level === 'epic') {
        filtered = filtered.filter(i => i.id === epicId);
      } else if (level === 'feature') {
        filtered = filtered.filter(i => i.parentId === epicId);
      } else if (level === 'user-story') {
        const featureIds = new Set(
          items.filter(i => i.type === 'feature' && i.parentId === epicId).map(i => i.id),
        );
        filtered = filtered.filter(i => i.parentId !== null && featureIds.has(i.parentId));
      }
    }

    if (level === 'user-story') {
      const keyword = this._filterKeyword().trim().toLowerCase();
      if (keyword) {
        filtered = filtered.filter(i => i.title.toLowerCase().includes(keyword));
      }

      const state = this._filterState();
      if (state) {
        filtered = filtered.filter(i => i.state === state);
      }

      const sprintFilter = this._filterSprintId();
      if (sprintFilter === 'unassigned') {
        filtered = filtered.filter(i => i.sprintId === null);
      } else if (sprintFilter) {
        filtered = filtered.filter(i => i.sprintId === sprintFilter);
      }
    }

    return filtered.slice().sort((a, b) => a.rank - b.rank);
  });

  /** The rendered slice of {@link visibleItems} — capped for a lighter first paint. Grows via {@link loadMore}. */
  readonly pagedItems = computed(() => this.visibleItems().slice(0, this._visibleCount()));

  /** True when more filtered rows exist beyond the currently rendered slice. */
  readonly hasMoreItems = computed(() => this._visibleCount() < this.visibleItems().length);

  readonly epics = computed(() =>
    this._items().filter(i => i.type === 'epic').sort((a, b) => a.rank - b.rank),
  );
  readonly features = computed(() =>
    this._items().filter(i => i.type === 'feature').sort((a, b) => a.rank - b.rank),
  );
  readonly stories = computed(() =>
    this._items().filter(i => i.type === 'user-story').sort((a, b) => a.rank - b.rank),
  );
  readonly readyStories = computed(() =>
    this.stories().filter(i => i.state === 'ready').length,
  );
  /** Number of active Product Backlog filters (keyword, state, sprint). */
  readonly activeFilterCount = computed(() => {
    let count = 0;
    if (this._filterKeyword().trim()) count++;
    if (this._filterState()) count++;
    if (this._filterSprintId()) count++;
    return count;
  });

  constructor() {
    this._loadTrigger$
      .pipe(
        switchMap(repoId => {
          if (!repoId) {
            this._items.set([]);
            this._loading.set(false);
            return EMPTY;
          }
          return this.http
            .get<BacklogItemApiDto[]>(this.baseUrl(repoId))
            .pipe(
              catchError(() => {
                this.toast.error('Failed to load backlog items.');
                this._loading.set(false);
                return of([]);
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(dtos => {
        this._items.set(this.flatten(dtos));
        this._loading.set(false);
      });

    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      this._loading.set(true);
      this._loadTrigger$.next(repoId);
      if (repoId) {
        this.http
          .get<SprintApiDto[]>(`${environment.apiBaseUrl}/repositories/${repoId}/sprints`)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({ next: s => this._sprints.set(s), error: () => {} });
      } else {
        this._sprints.set([]);
      }
    });
  }

  /** Renders the next batch of Product Backlog rows. */
  loadMore(): void {
    this._visibleCount.update(c => c + BacklogManagementService.PAGE_SIZE);
  }

  /** Resets the rendered slice back to the first page — called whenever the list context changes. */
  private resetPaging(): void {
    this._visibleCount.set(BacklogManagementService.PAGE_SIZE);
  }

  /** @param level The hierarchy level to display. */
  setLevel(level: BacklogLevel): void {
    this._activeLevel.set(level);
    this.resetPaging();
  }

  /** @param mode Fibonacci story-points or T-shirt sizing. */
  setEstimateMode(mode: EstimateMode): void {
    this._estimateMode.set(mode);
  }

  /** @param epicId Epic to drill into, or null to show all. */
  setSelectedEpic(epicId: string | null): void {
    this._selectedEpicId.set(epicId);
    this.resetPaging();
  }

  /** @param value Free-text search applied to the Product Backlog title. */
  setFilterKeyword(value: string): void {
    this._filterKeyword.set(value);
    this.resetPaging();
  }

  /** @param state Refinement state to filter by, or '' for all states. */
  setFilterState(state: BacklogState | ''): void {
    this._filterState.set(state);
    this.resetPaging();
  }

  /** @param sprintId Sprint ID to filter by, 'unassigned', or '' for all sprints. */
  setFilterSprintId(sprintId: string): void {
    this._filterSprintId.set(sprintId);
    this.resetPaging();
  }

  /** Resets all Product Backlog filters (keyword, state, sprint) to their defaults. */
  clearFilters(): void {
    this._filterKeyword.set('');
    this._filterState.set('');
    this._filterSprintId.set('');
    this.resetPaging();
  }

  /** Handles CDK drag-drop reorder, calling the rank API with prev/next sibling IDs. @param event The drop event from CdkDropList. */
  drop(event: CdkDragDrop<BacklogItem[]>): void {
    if (event.previousIndex === event.currentIndex) return;

    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const ordered = this.visibleItems().slice();
    const [moved] = ordered.splice(event.previousIndex, 1);
    ordered.splice(event.currentIndex, 0, moved);

    const prev = ordered[event.currentIndex - 1] ?? null;
    const next = ordered[event.currentIndex + 1] ?? null;

    // Defer signal update so CDK finishes cleaning up its preview element first,
    // then recompute rank optimistically without waiting for the API round-trip.
    const newRank = this.computeRank(prev?.rank ?? null, next?.rank ?? null);
    const snapshot = this._items();
    setTimeout(() => {
      this._items.update(items =>
        items.map(i => i.id === moved.id ? { ...i, rank: newRank } : i),
      );
    }, 0);

    this.rankItem(repoId, moved.id, prev?.id ?? null, next?.id ?? null, snapshot);
  }

  /** Moves an item to the top of the current level by ranking it before all siblings. @param itemId The item to move. */
  moveToTop(itemId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const ordered = this.visibleItems();
    const first   = ordered.find(i => i.id !== itemId);
    this.rankItem(repoId, itemId, null, first?.id ?? null);
  }

  /** Moves an item to a 1-based position in the current level. @param itemId The item to move. @param value String representation of the target position. */
  moveToPosition(itemId: string, value: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const position = Math.max(1, Number(value) || 1);
    const ordered  = this.visibleItems().filter(i => i.id !== itemId);
    const prev     = ordered[position - 2] ?? null;
    const next     = ordered[position - 1] ?? null;
    this.rankItem(repoId, itemId, prev?.id ?? null, next?.id ?? null);
  }

  /** Assigns or clears the sprint for an item, transitioning its state via the API. @param itemId The backlog item. @param sprintId Sprint ID, or empty string to unschedule. */
  moveToIteration(itemId: string, sprintId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const body: MoveToIterationApiRequest = { sprintId: sprintId || null };
    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/iteration`, body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update iteration.'),
      });
  }

  /** Reassigns the parent of an item. @param itemId The item whose parent changes. @param parentId New parent ID, or empty string for root. */
  updateParent(itemId: string, parentId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const item = this._items().find(i => i.id === itemId);
    if (!item) return;

    this.putItem(repoId, { ...item, parentId: parentId || null });
  }

  /**
   * Replaces the document list via the dedicated documents endpoint. Unlike {@link saveItemDetails},
   * this works regardless of refinement state — used for Committed items, where the general PUT
   * is rejected server-side.
   * @param itemId The target item.
   * @param documents Updated list of document titles or links.
   */
  updateDocuments(itemId: string, documents: string[]): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/documents`, { documents })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update documents.'),
      });
  }

  /**
   * Replaces the acceptance criteria via the dedicated endpoint. Unlike {@link saveItemDetails},
   * this works regardless of refinement state — used for Committed items, where the general PUT
   * is rejected server-side.
   * @param itemId The target item.
   * @param criteria New acceptance criteria list.
   */
  updateAcceptanceCriteria(itemId: string, criteria: string[]): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/acceptance-criteria`, { acceptanceCriteria: this.serializeAc(criteria) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update acceptance criteria.'),
      });
  }

  /**
   * Saves every editable field of a backlog item (title, state, sprint, estimate, documents,
   * acceptance criteria) via a single PUT. The backend rejects this when state is Committed —
   * use {@link updateTitle}, {@link updateDocuments} and {@link updateAcceptanceCriteria} instead
   * for a promoted item. Used by the item-detail dialog for non-Committed items.
   * @param itemId The target item.
   * @param changes Full set of edited field values to merge into the current item.
   */
  saveItemDetails(itemId: string, changes: {
    title: string;
    state: BacklogState;
    sprintId: string | null;
    storyPoints: number | null;
    tshirtSize: TshirtSize | null;
    documents: string[];
    acceptanceCriteria: string[];
  }): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const item = this._items().find(i => i.id === itemId);
    if (!item) return;

    this.putItem(repoId, { ...item, ...changes });
  }

  /** Renames a backlog item. @param itemId The target item. @param title New title. */
  updateTitle(itemId: string, title: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/title`, { title })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update title.'),
      });
  }

  /** Transitions a backlog item to a new refinement state. @param itemId The target item. @param state New state. */
  updateState(itemId: string, state: BacklogState): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/state`, { state: this.toApiState(state) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update state.'),
      });
  }

  /** Deletes a backlog item. Fails server-side if the item has children. @param itemId The item to delete. */
  deleteItem(itemId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http
      .delete(`${this.baseUrl(repoId)}/${itemId}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to delete item. It may still have children.'),
      });
  }

  /** Reloads the backlog list from the server. Called by the page component on every navigation. */
  reset(): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this._loading.set(true);
    this.reload(repoId);
  }

  /**
   * Creates a new backlog item of any hierarchy level.
   * @param type Epic, Feature, or User Story.
   * @param title Item title.
   * @param parentId Parent item ID, or null for a root-level item.
   */
  addItem(type: BacklogLevel, title: string, parentId: string | null): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const body: CreateBacklogItemApiRequest = {
      type:               this.toApiType(type),
      title,
      parentId:           parentId ?? null,
      acceptanceCriteria: '',
    };

    this.http
      .post<BacklogItemApiDto>(this.baseUrl(repoId), body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => {
          this._activeLevel.set(type);
          this.reload(repoId);
        },
        error: (err) => this.toast.error(err?.error?.error ?? `Failed to create ${this.levelLabel(type)}.`),
      });
  }

  /** @returns Human-readable label for the given level. @param level The hierarchy level. */
  levelLabel(level: BacklogLevel): string {
    switch (level) {
      case 'epic':       return 'Epics';
      case 'feature':    return 'Features';
      case 'user-story': return 'Product Backlog';
    }
  }

  /** @param item The item whose valid parent options to return. @returns List of items one level above. */
  parentOptions(item: BacklogItem): BacklogItem[] {
    if (item.type === 'epic') return [];
    const parentType: BacklogLevel = item.type === 'feature' ? 'epic' : 'feature';
    return this._items()
      .filter(i => i.type === parentType)
      .sort((a, b) => a.rank - b.rank);
  }

  /** @param item The item whose parent title to resolve. @returns Parent title, or "No parent" / "Missing parent". */
  parentTitle(item: BacklogItem): string {
    if (!item.parentId) return 'No parent';
    return this._items().find(i => i.id === item.parentId)?.title ?? 'Missing parent';
  }

  /** @param itemId The item whose children to count. @returns Number of direct children. */
  childCount(itemId: string): number {
    return this._items().filter(i => i.parentId === itemId).length;
  }

  /** Promotes a Ready UserStory to the given sprint, creating a SprintTask. @param itemId The backlog item. @param sprintId The target sprint. */
  promoteToSprint(itemId: string, sprintId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http
      .post<{ sprintTaskId: string }>(`${this.baseUrl(repoId)}/${itemId}/promote/${sprintId}`, {})
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: (err) => this.toast.error(err?.error?.error ?? 'Failed to promote item. Make sure it is a Ready user story.'),
      });
  }

  /**
   * Transitions multiple backlog items to the same refinement state in one request, then reloads.
   * @param ids The backlog item IDs to update.
   * @param state Target refinement state (Committed is rejected server-side).
   */
  bulkUpdateState(ids: string[], state: BacklogState): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || ids.length === 0) return;

    const body = { itemIds: ids, state: this.toApiState(state) };
    this.http
      .post<{ affected: number; skipped: number }>(`${this.baseUrl(repoId)}/bulk/state`, body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: r => {
          this.toast.success(this.bulkMessage(r.affected, r.skipped, 'updated'));
          this.reload(repoId);
        },
        error: err => this.toast.error(err?.error?.error ?? 'Failed to update items.'),
      });
  }

  /**
   * Deletes multiple backlog items in one request, then reloads. Items whose children are not also
   * selected are skipped server-side to avoid orphaning.
   * @param ids The backlog item IDs to delete.
   */
  bulkDelete(ids: string[]): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || ids.length === 0) return;

    this.http
      .post<{ affected: number; skipped: number }>(`${this.baseUrl(repoId)}/bulk/delete`, { itemIds: ids })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: r => {
          if (r.affected > 0) this.toast.success(this.bulkMessage(r.affected, r.skipped, 'deleted'));
          else this.toast.info('No items were deleted — selected parents still have children.');
          this.reload(repoId);
        },
        error: err => this.toast.error(err?.error?.error ?? 'Failed to delete items.'),
      });
  }

  // ── Private helpers ──────────────────────────────────────────────────────────

  /** Builds a toast summary line for a bulk operation. @param affected Count changed. @param skipped Count skipped. @param verb Past-tense verb (e.g. "updated"). */
  private bulkMessage(affected: number, skipped: number, verb: string): string {
    const base = `${affected} item${affected === 1 ? '' : 's'} ${verb}`;
    return skipped > 0 ? `${base} · ${skipped} skipped` : base;
  }

  private baseUrl(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/backlog`;
  }

  private reload(repoId: string): void {
    this._loadTrigger$.next(repoId);
  }

  /** Calls the rank API with prev/next sibling IDs, then reloads. */
  private computeRank(prevRank: number | null, nextRank: number | null): number {
    if (prevRank === null) return (nextRank ?? 1000) / 2;
    if (nextRank === null) return prevRank + 1000;
    return (prevRank + nextRank) / 2;
  }

  private rankItem(repoId: string, itemId: string, previousItemId: string | null, nextItemId: string | null, rollback?: BacklogItem[]): void {
    const body: RankBacklogItemApiRequest = { previousItemId, nextItemId };
    this.http
      .patch(`${this.baseUrl(repoId)}/${itemId}/rank`, body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => {
          if (rollback) this._items.set(rollback);
          this.toast.error('Failed to reorder item.');
        },
      });
  }

  /** Sends a full PUT update for the given local item snapshot. */
  private putItem(repoId: string, item: BacklogItem): void {
    const body: UpdateBacklogItemApiRequest = {
      title:              item.title,
      state:              this.toApiState(item.state),
      sprintId:           item.sprintId,
      storyPoints:        item.storyPoints,
      tshirtSize:         item.tshirtSize !== null ? this.toApiTshirt(item.tshirtSize) : null,
      acceptanceCriteria: this.serializeAc(item.acceptanceCriteria),
      documents:          item.documents,
    };

    this.http
      .put(`${this.baseUrl(repoId)}/${item.id}`, body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reload(repoId),
        error: () => this.toast.error('Failed to update item.'),
      });
  }

  /** Recursively flattens the API tree into a single list for signal state. */
  private flatten(dtos: BacklogItemApiDto[]): BacklogItem[] {
    const result: BacklogItem[] = [];
    const visit = (dto: BacklogItemApiDto): void => {
      result.push({
        id:                 dto.id,
        type:               this.toLocalType(dto.type),
        title:              dto.title,
        parentId:           dto.parentId,
        rank:               dto.rank,
        sprintId:           dto.sprintId,
        sprintName:         dto.sprintName,
        state:              this.toLocalState(dto.state),
        storyPoints:        dto.storyPoints,
        tshirtSize:         dto.tshirtSize !== null ? this.toLocalTshirt(dto.tshirtSize) : null,
        acceptanceCriteria: this.parseAc(dto.acceptanceCriteria),
        documents:          dto.documents,
      });
      dto.children.forEach(visit);
    };
    dtos.forEach(visit);
    return result;
  }

  /** Deserializes the stored JSON string into a criteria array. Falls back to wrapping legacy plain text. */
  private parseAc(raw: string): string[] {
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [raw];
    } catch {
      return [raw];
    }
  }

  /** Serializes a criteria array to a JSON string for storage. Empty items are filtered out. */
  private serializeAc(items: string[]): string {
    const filtered = items.filter(i => i.trim());
    return filtered.length ? JSON.stringify(filtered) : '';
  }

  private toLocalType(t: BacklogItemApiType): BacklogLevel {
    switch (t) {
      case BacklogItemApiType.Epic:      return 'epic';
      case BacklogItemApiType.Feature:   return 'feature';
      case BacklogItemApiType.UserStory: return 'user-story';
    }
  }

  private toApiType(t: BacklogLevel): BacklogItemApiType {
    switch (t) {
      case 'epic':       return BacklogItemApiType.Epic;
      case 'feature':    return BacklogItemApiType.Feature;
      case 'user-story': return BacklogItemApiType.UserStory;
    }
  }

  private toLocalState(s: BacklogItemApiState): BacklogState {
    switch (s) {
      case BacklogItemApiState.New:       return 'new';
      case BacklogItemApiState.Refining:  return 'refining';
      case BacklogItemApiState.Ready:     return 'ready';
      case BacklogItemApiState.Committed: return 'committed';
    }
  }

  private toApiState(s: BacklogState): BacklogItemApiState {
    switch (s) {
      case 'new':       return BacklogItemApiState.New;
      case 'refining':  return BacklogItemApiState.Refining;
      case 'ready':     return BacklogItemApiState.Ready;
      case 'committed': return BacklogItemApiState.Committed;
    }
  }

  private toLocalTshirt(t: BacklogTshirtSize): TshirtSize {
    switch (t) {
      case BacklogTshirtSize.XS: return 'XS';
      case BacklogTshirtSize.S:  return 'S';
      case BacklogTshirtSize.M:  return 'M';
      case BacklogTshirtSize.L:  return 'L';
      case BacklogTshirtSize.XL: return 'XL';
    }
  }

  private toApiTshirt(t: TshirtSize): BacklogTshirtSize {
    switch (t) {
      case 'XS': return BacklogTshirtSize.XS;
      case 'S':  return BacklogTshirtSize.S;
      case 'M':  return BacklogTshirtSize.M;
      case 'L':  return BacklogTshirtSize.L;
      case 'XL': return BacklogTshirtSize.XL;
    }
  }
}
