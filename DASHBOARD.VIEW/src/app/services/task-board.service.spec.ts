import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TaskBoardService } from './task-board.service';
import { RepositoryContextService } from './repository-context.service';
import { ResourceService } from './resource.service';
import { AuthService } from './auth.service';
import { ToastService } from '../core/components/toast/toast.service';
import { environment } from '../../environments/environment';
import {
  WorkItemApiPriority,
  WorkItemApiState,
  WorkItemApiType,
  WorkItemSummaryApiDto,
} from '../models/work-item-api.model';
import { UserProfile } from '../models/user.model';

/**
 * Unit tests for TaskBoardService — the filtering and column split behind the Kanban board.
 *
 * Two pieces of derived state carry the weight here. `filteredTasks` folds four independent filters
 * together, and a second effect splits its result into the three board columns. Because the split
 * runs off the *filtered* list rather than the raw one, a filter that silently stopped applying
 * would not show up as an error — the board would simply display cards the user had filtered out,
 * and the counts beside each column heading would agree with the wrong list.
 *
 * The "assigned to me" filter is the one worth extra care: it compares against the signed-in user's
 * id, so it depends on AuthService. If nobody is signed in the comparison is against null, which
 * happens to match every unassigned card — that is pinned below rather than left to chance.
 */
describe('TaskBoardService', () => {
  let service: TaskBoardService;
  let httpMock: HttpTestingController;
  let selectedRepoId: ReturnType<typeof signal<string | null>>;
  let currentUser: ReturnType<typeof signal<UserProfile | null>>;

  const repoId    = 'repo-1';
  const itemsUrl  = `${environment.apiBaseUrl}/repositories/${repoId}/workitems?pageSize=100`;

  const me: UserProfile = {
    userId: 'user-me', email: 'nam@acme.local', name: 'Nam',
    avatarClass: 'bg-sky-600', isGlobalAdmin: false, orgId: 'org-1', orgAlias: 'acme',
  };

  function dto(
    id: string,
    over: Partial<WorkItemSummaryApiDto> = {},
  ): WorkItemSummaryApiDto {
    return {
      id, workItemNumber: 1, customId: `DASH-${id}`,
      workItemType: WorkItemApiType.Task, title: `Item ${id}`,
      priority: WorkItemApiPriority.Medium, state: WorkItemApiState.New,
      assignedToId: null, assignedToName: null, assignedToAvatar: null,
      sprint: 'Sprint 1', createdAt: '2026-06-01T00:00:00Z',
      ...over,
    };
  }

  /**
   * A board with one card per column plus variations the filters can bite on.
   *
   *   T1  todo         · task    · medium · unassigned
   *   T2  in-progress  · bug     · high   · assigned to me
   *   T3  done         · task    · low    · assigned to someone else
   *   T4  todo         · bug     · high   · assigned to me
   */
  function board(): WorkItemSummaryApiDto[] {
    return [
      dto('T1', { title: 'Checkout flow', state: WorkItemApiState.New }),
      dto('T2', {
        title: 'Login fails on Safari', state: WorkItemApiState.Active,
        workItemType: WorkItemApiType.Bug, priority: WorkItemApiPriority.High,
        assignedToId: 'user-me',
      }),
      dto('T3', {
        title: 'Audit export', state: WorkItemApiState.Closed,
        priority: WorkItemApiPriority.Low, assignedToId: 'user-other',
      }),
      dto('T4', {
        title: 'Login rework', state: WorkItemApiState.New,
        workItemType: WorkItemApiType.Bug, priority: WorkItemApiPriority.High,
        assignedToId: 'user-me',
      }),
    ];
  }

  /** Selects the repository and flushes the work-item request the effect fires. */
  function load(items: WorkItemSummaryApiDto[], status?: number): void {
    selectedRepoId.set(repoId);
    TestBed.flushEffects();

    const request = httpMock.expectOne(itemsUrl);
    if (status) request.flush({}, { status, statusText: 'Error' });
    else request.flush({ items, totalCount: items.length, page: 1, pageSize: 100 });

    // The column split runs in its own effect off filteredTasks.
    TestBed.flushEffects();
  }

  /** Re-runs the column-splitting effect after a filter change. */
  function applyFilters(): void {
    TestBed.flushEffects();
  }

  function ids(items: { id: string }[]): string[] {
    return items.map(i => i.id);
  }

  beforeEach(() => {
    selectedRepoId = signal<string | null>(null);
    currentUser    = signal<UserProfile | null>(me);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        TaskBoardService,
        { provide: RepositoryContextService, useValue: { selectedRepoId } },
        { provide: AuthService, useValue: { currentUser } },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
        { provide: ResourceService, useValue: { get: (k: string, fallback = k) => fallback } },
      ],
    });

    service  = TestBed.inject(TaskBoardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // ── Loading ─────────────────────────────────────────────────────────────

  describe('loading', () => {
    it('maps the API page into work items', () => {
      load(board());

      expect(ids(service.workItems())).toEqual(['T1', 'T2', 'T3', 'T4']);
      expect(service.loading()).toBeFalse();
    });

    it('translates the API state into a board status', () => {
      load(board());

      const byId = (id: string) => service.workItems().find(t => t.id === id);
      expect(byId('T1')?.status).toBe('todo');
      expect(byId('T2')?.status).toBe('in-progress');
      expect(byId('T3')?.status).toBe('done');
    });

    it('translates the API priority', () => {
      load(board());

      expect(service.workItems().find(t => t.id === 'T2')?.priority).toBe('high');
      expect(service.workItems().find(t => t.id === 'T3')?.priority).toBe('low');
    });

    it('surfaces an error message when the request fails', () => {
      load([], 500);

      expect(service.error()).toBe('Failed to load work items');
      expect(service.loading()).toBeFalse();
    });

    it('does not request anything until a repository is selected', () => {
      // Nothing flushed: httpMock.verify() in afterEach fails if a stray request was made.
      expect(service.workItems()).toEqual([]);
    });
  });

  // ── The column split ────────────────────────────────────────────────────

  describe('the board columns', () => {
    beforeEach(() => load(board()));

    it('place each card in the column its status maps to', () => {
      expect(ids(service.todoColumn())).toEqual(['T1', 'T4']);
      expect(ids(service.inProgressColumn())).toEqual(['T2']);
      expect(ids(service.doneColumn())).toEqual(['T3']);
    });

    it('account for every card exactly once', () => {
      const total = service.todoColumn().length
                  + service.inProgressColumn().length
                  + service.doneColumn().length;

      expect(total).toBe(service.workItems().length);
    });

    it('are addressable by status through columnSignal', () => {
      expect(ids(service.columnSignal('todo')())).toEqual(['T1', 'T4']);
      expect(ids(service.columnSignal('in-progress')())).toEqual(['T2']);
      expect(ids(service.columnSignal('done')())).toEqual(['T3']);
    });

    it('follow the filters rather than the raw list', () => {
      // This is the coupling that matters: the columns are built from filteredTasks, so a filter
      // must empty the board, not just the count beside it.
      service.searchQuery.set('audit');
      applyFilters();

      expect(ids(service.todoColumn())).toEqual([]);
      expect(ids(service.doneColumn())).toEqual(['T3']);
    });
  });

  describe('the column counts', () => {
    beforeEach(() => load(board()));

    it('match the number of cards in each column', () => {
      expect(service.todoCount()).toBe(2);
      expect(service.inProgressCount()).toBe(1);
      expect(service.doneCount()).toBe(1);
    });

    it('reflect the active filters, so heading and content agree', () => {
      service.priorityFilter.set('high');
      applyFilters();

      expect(service.todoCount()).toBe(1);
      expect(service.todoColumn().length).toBe(1);
      expect(service.doneCount()).toBe(0);
    });
  });

  // ── Filters ─────────────────────────────────────────────────────────────

  describe('the search filter', () => {
    beforeEach(() => load(board()));

    it('matches the title case-insensitively', () => {
      service.searchQuery.set('LOGIN');

      expect(ids(service.filteredTasks())).toEqual(['T2', 'T4']);
    });

    it('also matches the work item id', () => {
      // Pasting an id from a chat message is the fastest way to find a card.
      service.searchQuery.set('T3');

      expect(ids(service.filteredTasks())).toEqual(['T3']);
    });

    it('ignores surrounding whitespace', () => {
      service.searchQuery.set('   audit   ');

      expect(ids(service.filteredTasks())).toEqual(['T3']);
    });

    it('treats a whitespace-only query as no filter', () => {
      service.searchQuery.set('   ');

      expect(service.filteredTasks().length).toBe(4);
    });

    it('returns nothing when there is no match', () => {
      service.searchQuery.set('nonexistent');

      expect(service.filteredTasks()).toEqual([]);
    });
  });

  describe('the priority filter', () => {
    beforeEach(() => load(board()));

    it('keeps only cards at that priority', () => {
      service.priorityFilter.set('high');

      expect(ids(service.filteredTasks())).toEqual(['T2', 'T4']);
    });

    it('treats "all" as no filter', () => {
      service.priorityFilter.set('high');
      service.priorityFilter.set('all');

      expect(service.filteredTasks().length).toBe(4);
    });
  });

  describe('the type filter', () => {
    beforeEach(() => load(board()));

    it('keeps only cards of that type', () => {
      service.workItemTypeFilter.set('bug');

      expect(ids(service.filteredTasks())).toEqual(['T2', 'T4']);
    });

    it('treats "all" as no filter', () => {
      service.workItemTypeFilter.set('bug');
      service.workItemTypeFilter.set('all');

      expect(service.filteredTasks().length).toBe(4);
    });
  });

  describe('the assigned-to-me filter', () => {
    it('keeps only the signed-in user cards', () => {
      load(board());

      service.assignedToMeOnly.set(true);

      expect(ids(service.filteredTasks())).toEqual(['T2', 'T4']);
    });

    it('excludes unassigned cards', () => {
      load(board());

      service.assignedToMeOnly.set(true);

      expect(ids(service.filteredTasks())).not.toContain('T1');
    });

    it('matches unassigned cards when nobody is signed in', () => {
      // The comparison is against `currentUser()?.userId ?? null`, so with no session it matches
      // cards whose assignedToId is also null. Pinned deliberately: it is the current behaviour
      // and the filter is only reachable from a signed-in board, but a reader should not have to
      // guess what happens here.
      currentUser.set(null);
      load(board());

      service.assignedToMeOnly.set(true);

      expect(ids(service.filteredTasks())).toEqual(['T1']);
    });

    it('is toggled by toggleAssignedToMe', () => {
      load(board());

      service.toggleAssignedToMe();
      expect(service.assignedToMeOnly()).toBeTrue();

      service.toggleAssignedToMe();
      expect(service.assignedToMeOnly()).toBeFalse();
    });
  });

  describe('combining filters', () => {
    beforeEach(() => load(board()));

    it('applies them together rather than as alternatives', () => {
      service.workItemTypeFilter.set('bug');
      service.assignedToMeOnly.set(true);
      service.searchQuery.set('rework');

      expect(ids(service.filteredTasks())).toEqual(['T4']);
    });

    it('can exclude everything', () => {
      service.priorityFilter.set('critical');

      expect(service.filteredTasks()).toEqual([]);
    });
  });

  describe('hasActiveFilters', () => {
    beforeEach(() => load(board()));

    it('is false with nothing set', () => {
      expect(service.hasActiveFilters()).toBeFalse();
    });

    it('is true for a search query', () => {
      service.searchQuery.set('login');

      expect(service.hasActiveFilters()).toBeTrue();
    });

    it('is false for a whitespace-only query', () => {
      // Otherwise the "clear filters" affordance would appear while nothing was filtered.
      service.searchQuery.set('   ');

      expect(service.hasActiveFilters()).toBeFalse();
    });

    it('is true for each of the other three filters', () => {
      service.priorityFilter.set('high');
      expect(service.hasActiveFilters()).toBeTrue();

      service.priorityFilter.set('all');
      service.workItemTypeFilter.set('bug');
      expect(service.hasActiveFilters()).toBeTrue();

      service.workItemTypeFilter.set('all');
      service.assignedToMeOnly.set(true);
      expect(service.hasActiveFilters()).toBeTrue();
    });
  });

  // ── Dialog state ────────────────────────────────────────────────────────

  describe('the detail dialog', () => {
    beforeEach(() => load(board()));

    it('resolves dialogTask from the open id', () => {
      service.dialogTaskId.set('T2');

      expect(service.dialogTask()?.id).toBe('T2');
    });

    it('is null when no dialog is open', () => {
      expect(service.dialogTask()).toBeNull();
    });

    it('is null when the open id no longer exists', () => {
      // The card can be deleted in another tab while its dialog is open; the template must get a
      // null rather than a stale object.
      service.dialogTaskId.set('T-deleted');

      expect(service.dialogTask()).toBeNull();
    });

    it('opens in create mode with the requested type', () => {
      service.openCreate('bug');

      expect(service.dialogMode()).toBe('create');
      expect(service.createInitialType()).toBe('bug');
      expect(service.dialogTaskId()).toBeNull();
    });

    it('closes cleanly', () => {
      service.openCreate('task');
      service.closeDialog();

      expect(service.dialogMode()).toBeNull();
      expect(service.dialogTaskId()).toBeNull();
    });
  });
});
