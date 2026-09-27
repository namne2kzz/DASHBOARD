import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { BacklogManagementService } from './backlog-management.service';
import { RepositoryContextService } from './repository-context.service';
import { PrivilegeService } from '../core/services/privilege.service';
import { ToastService } from '../core/components/toast/toast.service';
import { environment } from '../../environments/environment';
import {
  BacklogItemApiDto,
  BacklogItemApiState,
  BacklogItemApiType,
} from '../models/backlog-api.model';

/**
 * Unit tests for BacklogManagementService — the filtering, drill-down and paging behind the
 * Product Backlog page.
 *
 * This service holds 683 lines and makes no HTTP call of its own beyond the initial load: almost
 * everything in it is derived state. `visibleItems` alone folds together the hierarchy level, an
 * epic drill-down that reaches through the feature layer, three independent filters and a rank sort,
 * and `pagedItems` then slices it. None of that is covered anywhere else — the backend tests the
 * tree it *sends*, not the tree the browser renders from it.
 *
 * The drill-down deserves the attention it gets below. Selecting an epic while viewing user stories
 * cannot filter on `parentId === epicId`, because a story's parent is a feature, not an epic. The
 * service resolves the feature layer first; a regression there would quietly show an empty backlog
 * for an epic that has plenty of stories.
 */
describe('BacklogManagementService', () => {
  let service: BacklogManagementService;
  let httpMock: HttpTestingController;
  let selectedRepoId: ReturnType<typeof signal<string | null>>;
  let toast: jasmine.SpyObj<ToastService>;

  const repoId      = 'repo-1';
  const backlogUrl  = `${environment.apiBaseUrl}/repositories/${repoId}/backlog`;
  const sprintsUrl  = `${environment.apiBaseUrl}/repositories/${repoId}/sprints`;

  /** Builds an API DTO. Children are nested exactly as the API returns them. */
  function dto(
    id: string,
    type: BacklogItemApiType,
    over: Partial<BacklogItemApiDto> = {},
  ): BacklogItemApiDto {
    return {
      id, repositoryId: repoId, type, title: `Item ${id}`,
      parentId: null, rank: 1000, sprintId: null, sprintName: null,
      state: BacklogItemApiState.New, storyPoints: null, tshirtSize: null,
      acceptanceCriteria: '', documents: [], createdAt: '2026-06-01T00:00:00Z',
      children: [],
      ...over,
    };
  }

  /**
   * A three-level backlog: one epic → two features → four stories, plus a second epic whose story
   * must never leak into the first epic's drill-down.
   *
   *   E1 ─ F1 ─ S1 (ready,  sprint-1)
   *      │    └ S2 (new,    unassigned)
   *      └ F2 ─ S3 (ready,  unassigned)
   *   E2 ─ F3 ─ S4 (committed, sprint-1)
   */
  function tree(): BacklogItemApiDto[] {
    return [
      dto('E1', BacklogItemApiType.Epic, { rank: 1000, children: [
        dto('F1', BacklogItemApiType.Feature, { parentId: 'E1', rank: 1000, children: [
          dto('S1', BacklogItemApiType.UserStory, {
            parentId: 'F1', rank: 1000, title: 'Checkout flow',
            state: BacklogItemApiState.Ready, sprintId: 'sprint-1', sprintName: 'Sprint 1',
          }),
          dto('S2', BacklogItemApiType.UserStory, {
            parentId: 'F1', rank: 2000, title: 'Login rework',
            state: BacklogItemApiState.New,
          }),
        ] }),
        dto('F2', BacklogItemApiType.Feature, { parentId: 'E1', rank: 2000, children: [
          dto('S3', BacklogItemApiType.UserStory, {
            parentId: 'F2', rank: 3000, title: 'Password reset',
            state: BacklogItemApiState.Ready,
          }),
        ] }),
      ] }),
      dto('E2', BacklogItemApiType.Epic, { rank: 2000, children: [
        dto('F3', BacklogItemApiType.Feature, { parentId: 'E2', rank: 3000, children: [
          dto('S4', BacklogItemApiType.UserStory, {
            parentId: 'F3', rank: 4000, title: 'Audit export',
            state: BacklogItemApiState.Committed, sprintId: 'sprint-1', sprintName: 'Sprint 1',
          }),
        ] }),
      ] }),
      // A third epic with a feature but no stories under it, for the empty drill-down case.
      dto('E3', BacklogItemApiType.Epic, { rank: 3000, children: [
        dto('F4', BacklogItemApiType.Feature, { parentId: 'E3', rank: 4000 }),
      ] }),
    ];
  }

  /**
   * Selects the repository, which triggers both the backlog and the sprint request.
   *
   * The service loads from an `effect` on `selectedRepoId`, and effects do not run merely because
   * a signal changed — they are scheduled. `flushEffects` runs them synchronously so the request
   * exists by the time it is expected; without it every test here fails with "found none" on a
   * request the service really does make in the browser.
   */
  function load(items: BacklogItemApiDto[], options: { failBacklog?: boolean } = {}): void {
    selectedRepoId.set(repoId);
    TestBed.flushEffects();

    const backlog = httpMock.expectOne(backlogUrl);
    if (options.failBacklog) backlog.flush({}, { status: 500, statusText: 'Error' });
    else backlog.flush(items);

    httpMock.expectOne(sprintsUrl).flush([]);
  }

  function ids(items: { id: string }[]): string[] {
    return items.map(i => i.id);
  }

  beforeEach(() => {
    selectedRepoId = signal<string | null>(null);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        BacklogManagementService,
        { provide: RepositoryContextService, useValue: { selectedRepoId } },
        {
          provide: PrivilegeService,
          useValue: {
            canManageBacklog:  signal(true),
            canCreateWorkItem: signal(true),
            canEditWorkItem:   signal(true),
          },
        },
        { provide: ToastService, useValue: toast },
      ],
    });

    service  = TestBed.inject(BacklogManagementService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // ── Flattening the API tree ─────────────────────────────────────────────

  describe('loading', () => {
    it('flattens the nested API tree into a single list', () => {
      load(tree());

      // Three epics, four features and four stories.
      expect(service.items().length).toBe(11);
    });

    it('keeps every level addressable by type', () => {
      load(tree());

      expect(ids(service.epics())).toEqual(['E1', 'E2', 'E3']);
      expect(ids(service.features())).toEqual(['F1', 'F2', 'F3', 'F4']);
      expect(ids(service.stories())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });

    it('preserves the parent link through the flattening', () => {
      // The tree structure is only recoverable afterwards through parentId, so losing it here
      // would break every drill-down at once.
      load(tree());

      const story = service.items().find(i => i.id === 'S1');
      expect(story?.parentId).toBe('F1');
    });

    it('maps the numeric API enums to their local string form', () => {
      load(tree());

      const story = service.items().find(i => i.id === 'S1');
      expect(story?.type).toBe('user-story');
      expect(story?.state).toBe('ready');
    });

    it('reports an empty backlog rather than failing', () => {
      load([]);

      expect(service.items()).toEqual([]);
      expect(service.visibleItems()).toEqual([]);
    });

    it('shows a toast and leaves the list empty when the request fails', () => {
      load([], { failBacklog: true });

      expect(toast.error).toHaveBeenCalledWith('Failed to load backlog items.');
      expect(service.items()).toEqual([]);
    });

    it('clears the list when the repository is deselected', () => {
      load(tree());
      expect(service.items().length).toBe(11);

      selectedRepoId.set(null);
      TestBed.flushEffects();

      expect(service.items()).toEqual([]);
    });
  });

  // ── Level switching ─────────────────────────────────────────────────────

  describe('visibleItems by level', () => {
    beforeEach(() => load(tree()));

    it('defaults to user stories', () => {
      expect(service.activeLevel()).toBe('user-story');
      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });

    it('shows only epics at the epic level', () => {
      service.setLevel('epic');

      expect(ids(service.visibleItems())).toEqual(['E1', 'E2', 'E3']);
    });

    it('shows only features at the feature level', () => {
      service.setLevel('feature');

      expect(ids(service.visibleItems())).toEqual(['F1', 'F2', 'F3', 'F4']);
    });

    it('sorts by rank, not by the order the API nested them', () => {
      expect(service.visibleItems().map(i => i.rank)).toEqual([1000, 2000, 3000, 4000]);
    });
  });

  // ── Epic drill-down ─────────────────────────────────────────────────────

  describe('drilling into an epic', () => {
    beforeEach(() => load(tree()));

    it('shows just that epic at the epic level', () => {
      service.setLevel('epic');
      service.setSelectedEpic('E1');

      expect(ids(service.visibleItems())).toEqual(['E1']);
    });

    it('shows the epic own features at the feature level', () => {
      service.setLevel('feature');
      service.setSelectedEpic('E1');

      expect(ids(service.visibleItems())).toEqual(['F1', 'F2']);
    });

    it('reaches through the feature layer to find the stories', () => {
      // The heart of it: a story's parent is a feature, so filtering stories on
      // `parentId === 'E1'` would return nothing at all. The service resolves E1's features first.
      service.setSelectedEpic('E1');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3']);
    });

    it('excludes stories belonging to a different epic', () => {
      service.setSelectedEpic('E1');

      expect(ids(service.visibleItems())).not.toContain('S4');
    });

    it('shows everything again once the drill-down is cleared', () => {
      service.setSelectedEpic('E1');
      service.setSelectedEpic(null);

      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });

    it('returns nothing for an epic with no features', () => {
      service.setSelectedEpic('E-empty');

      expect(service.visibleItems()).toEqual([]);
    });

    it('returns nothing for an epic whose features have no stories', () => {
      // Distinct from the case above: E3's feature layer resolves to a real feature, there is just
      // nothing hanging off it. An implementation that fell back to "show everything" when the
      // story filter came up empty would surface the other epics' stories instead.
      service.setSelectedEpic('E3');

      expect(ids(service.features())).toContain('F4');
      expect(service.visibleItems()).toEqual([]);
    });
  });

  // ── Filters, story level only ───────────────────────────────────────────

  describe('the keyword filter', () => {
    beforeEach(() => load(tree()));

    it('matches on the title, case-insensitively', () => {
      service.setFilterKeyword('LOGIN');

      expect(ids(service.visibleItems())).toEqual(['S2']);
    });

    it('matches a substring anywhere in the title', () => {
      service.setFilterKeyword('flow');

      expect(ids(service.visibleItems())).toEqual(['S1']);
    });

    it('ignores surrounding whitespace', () => {
      service.setFilterKeyword('   login   ');

      expect(ids(service.visibleItems())).toEqual(['S2']);
    });

    it('treats a whitespace-only keyword as no filter', () => {
      service.setFilterKeyword('   ');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });

    it('returns nothing when nothing matches', () => {
      service.setFilterKeyword('nonexistent');

      expect(service.visibleItems()).toEqual([]);
    });

    it('does not apply at the epic level', () => {
      // The filter bar only renders on the Product Backlog (story) tab, so a keyword left over
      // from that tab must not silently hide epics.
      service.setFilterKeyword('login');
      service.setLevel('epic');

      expect(ids(service.visibleItems())).toEqual(['E1', 'E2', 'E3']);
    });
  });

  describe('the state filter', () => {
    beforeEach(() => load(tree()));

    it('keeps only items in that state', () => {
      service.setFilterState('ready');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S3']);
    });

    it('treats the empty string as all states', () => {
      service.setFilterState('ready');
      service.setFilterState('');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });
  });

  describe('the sprint filter', () => {
    beforeEach(() => load(tree()));

    it('keeps only items planned into that sprint', () => {
      service.setFilterSprintId('sprint-1');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S4']);
    });

    it('has a dedicated value for items in no sprint at all', () => {
      // 'unassigned' cannot be matched against sprintId directly — it is a sentinel meaning null.
      service.setFilterSprintId('unassigned');

      expect(ids(service.visibleItems())).toEqual(['S2', 'S3']);
    });

    it('treats the empty string as all sprints', () => {
      service.setFilterSprintId('sprint-1');
      service.setFilterSprintId('');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });
  });

  describe('combining filters', () => {
    beforeEach(() => load(tree()));

    it('applies them together, not as alternatives', () => {
      service.setFilterState('ready');
      service.setFilterSprintId('unassigned');

      // S3 is the only story that is both ready and unassigned.
      expect(ids(service.visibleItems())).toEqual(['S3']);
    });

    it('can combine with an epic drill-down', () => {
      service.setSelectedEpic('E1');
      service.setFilterState('ready');

      expect(ids(service.visibleItems())).toEqual(['S1', 'S3']);
    });

    it('can end up with no matches at all', () => {
      service.setFilterState('committed');
      service.setSelectedEpic('E1');

      // E1 has no committed story.
      expect(service.visibleItems()).toEqual([]);
    });
  });

  describe('activeFilterCount', () => {
    beforeEach(() => load(tree()));

    it('is zero with no filters set', () => {
      expect(service.activeFilterCount()).toBe(0);
    });

    it('counts each filter that is set', () => {
      service.setFilterKeyword('login');
      service.setFilterState('new');
      service.setFilterSprintId('sprint-1');

      expect(service.activeFilterCount()).toBe(3);
    });

    it('does not count a whitespace-only keyword', () => {
      // Otherwise the badge would show "1 filter" while the list was unfiltered.
      service.setFilterKeyword('   ');

      expect(service.activeFilterCount()).toBe(0);
    });

    it('does not count the epic drill-down', () => {
      // The drill-down has its own breadcrumb in the UI; counting it here would make the badge
      // disagree with the filter bar.
      service.setSelectedEpic('E1');

      expect(service.activeFilterCount()).toBe(0);
    });
  });

  describe('clearFilters', () => {
    beforeEach(() => load(tree()));

    it('resets all three filters', () => {
      service.setFilterKeyword('login');
      service.setFilterState('new');
      service.setFilterSprintId('sprint-1');

      service.clearFilters();

      expect(service.activeFilterCount()).toBe(0);
      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3', 'S4']);
    });

    it('leaves the epic drill-down in place', () => {
      service.setSelectedEpic('E1');
      service.setFilterState('ready');

      service.clearFilters();

      expect(service.selectedEpicId()).toBe('E1');
      expect(ids(service.visibleItems())).toEqual(['S1', 'S2', 'S3']);
    });
  });

  // ── Paging ──────────────────────────────────────────────────────────────

  describe('paging', () => {
    /** Loads 60 stories under one feature, enough for three pages of 25. */
    function loadManyStories(): void {
      const stories = Array.from({ length: 60 }, (_, i) =>
        dto(`S${i}`, BacklogItemApiType.UserStory, {
          parentId: 'F1', rank: (i + 1) * 1000, title: `Story ${i}`,
        }),
      );

      load([
        dto('E1', BacklogItemApiType.Epic, { children: [
          dto('F1', BacklogItemApiType.Feature, { parentId: 'E1', children: stories }),
        ] }),
      ]);
    }

    it('renders only the first page initially', () => {
      loadManyStories();

      expect(service.visibleItems().length).toBe(60);
      expect(service.pagedItems().length).toBe(25);
    });

    it('grows by one page per loadMore', () => {
      loadManyStories();

      service.loadMore();
      expect(service.pagedItems().length).toBe(50);

      service.loadMore();
      // Capped at what is actually there rather than running past the end.
      expect(service.pagedItems().length).toBe(60);
    });

    it('reports whether more rows remain', () => {
      loadManyStories();

      expect(service.hasMoreItems()).toBeTrue();

      service.loadMore();
      service.loadMore();

      expect(service.hasMoreItems()).toBeFalse();
    });

    it('is false when everything already fits on one page', () => {
      load(tree());

      expect(service.hasMoreItems()).toBeFalse();
    });

    it('resets to the first page when a filter changes', () => {
      // Otherwise a user who paged deep into an unfiltered list would apply a filter and see a
      // long stretch of nothing, because the slice still started far down the old list.
      loadManyStories();
      service.loadMore();
      service.loadMore();
      expect(service.pagedItems().length).toBe(60);

      service.setFilterKeyword('Story');

      expect(service.pagedItems().length).toBe(25);
    });

    it('resets to the first page when the level changes', () => {
      loadManyStories();
      service.loadMore();

      service.setLevel('feature');
      service.setLevel('user-story');

      expect(service.pagedItems().length).toBe(25);
    });

    it('resets to the first page when the epic drill-down changes', () => {
      loadManyStories();
      service.loadMore();

      service.setSelectedEpic('E1');

      expect(service.pagedItems().length).toBe(25);
    });

    it('resets when the filters are cleared', () => {
      loadManyStories();
      service.loadMore();

      service.clearFilters();

      expect(service.pagedItems().length).toBe(25);
    });

    it('keeps pagedItems in rank order', () => {
      loadManyStories();

      const ranks = service.pagedItems().map(i => i.rank);
      expect(ranks).toEqual([...ranks].sort((a, b) => a - b));
    });
  });

  // ── Derived counts ──────────────────────────────────────────────────────

  describe('readyStories', () => {
    it('counts stories in the ready state across the whole backlog', () => {
      load(tree());

      expect(service.readyStories()).toBe(2);
    });

    it('ignores the active filters', () => {
      // This is a backlog-health figure shown next to the tabs, not a count of what is on screen.
      load(tree());
      service.setFilterState('committed');

      expect(service.readyStories()).toBe(2);
    });

    it('ignores features and epics even if they share the state', () => {
      load(tree());

      // Only user stories count, never features or epics.
      expect(service.readyStories()).toBe(2);
    });
  });
});
