import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { SprintPlanningService } from './sprint-planning.service';
import { RepositoryContextService } from './repository-context.service';
import { SprintSelectionService } from './sprint-selection.service';
import { PrivilegeService } from '../core/services/privilege.service';
import { ToastService } from '../core/components/toast/toast.service';
import { environment } from '../../environments/environment';
import {
  DayOffApiDto,
  MemberLoadApiDto,
  SprintApiDto,
  SprintDetailApiDto,
  SprintTaskApiDto,
  SprintTaskApiType,
} from '../models/sprint-planning-api.model';

/**
 * Unit tests for SprintPlanningService — the capacity side of sprint planning.
 *
 * Worth being precise about what is under test here. Per-member capacity is computed on the server
 * and arrives ready-made in `memberLoads`, so this service only maps it; the arithmetic behind it is
 * covered by `GetSprintDetailQueryHandlerTests` on the backend. What *is* computed here, and has no
 * test anywhere else, is the team-level roll-up (`totalCapacity` → `teamLoadPercent` →
 * `teamLoadState`) and `effectiveWorkingDays`.
 *
 * Both of those re-implement rules the backend already got wrong once: the sentinel percentage when
 * capacity is zero, and subtracting team-wide days off from the working-day count. The backend has
 * 19 tests pinning its version. These pin the browser's, so the two cannot drift apart silently —
 * a page that shows a different number from the API is a bug report nobody can reproduce.
 */
describe('SprintPlanningService', () => {
  let service: SprintPlanningService;
  let httpMock: HttpTestingController;

  let selectedRepoId: ReturnType<typeof signal<string | null>>;
  let selectedSprintId: ReturnType<typeof signal<string | null>>;
  let sprints: ReturnType<typeof signal<SprintApiDto[]>>;
  let selectionLoading: ReturnType<typeof signal<boolean>>;

  const repoId   = 'repo-1';
  const sprintId = 'sprint-1';

  const detailUrl = (sprint = sprintId) =>
    `${environment.apiBaseUrl}/repositories/${repoId}/sprints/${sprint}/detail`;

  function memberLoad(over: Partial<MemberLoadApiDto> = {}): MemberLoadApiDto {
    return {
      userId: 'user-1', userName: 'Nam', userAvatar: 'bg-sky-600',
      hoursPerDay: 8, overtimeHoursPerDay: 0, personalDaysOffHours: 0,
      capacity: 80, workload: 40, loadPercent: 50, loadState: 'safe',
      ...over,
    };
  }

  function dayOff(over: Partial<DayOffApiDto> = {}): DayOffApiDto {
    return {
      id: `off-${Math.random()}`, sprintId, userId: null, userName: null,
      date: '2026-06-03', hours: 8, reason: 'Public holiday',
      ...over,
    };
  }

  /**
   * A sprint running Mon 1 Jun 2026 → Fri 12 Jun 2026: ten working days across two full weeks,
   * with no partial week at either end so the weekend filter is unambiguous.
   */
  function detail(over: Partial<SprintDetailApiDto> = {}): SprintDetailApiDto {
    return {
      id: sprintId, repositoryId: repoId, name: 'Sprint 1',
      startDate: '2026-06-01', endDate: '2026-06-12', isActive: true,
      workingDays: 10,
      tasks: [], capacityMembers: [], daysOff: [], memberLoads: [],
      ...over,
    };
  }

  function task(over: Partial<SprintTaskApiDto> = {}): SprintTaskApiDto {
    return {
      id: 'task-1', parentId: null, type: SprintTaskApiType.Task,
      workItemNumber: 'DASH-1', title: 'A task', description: '',
      priority: 2, assignedToId: null, assignedToName: null, state: 3,
      storyPoints: 0, originalEstimate: 0, remainingWork: 0, completedWork: 0,
      subTasks: [],
      ...over,
    } as SprintTaskApiDto;
  }

  /** Loads a sprint detail through the public refresh() path and flushes the response. */
  function load(payload: SprintDetailApiDto | null, status?: number): void {
    selectedRepoId.set(repoId);
    selectedSprintId.set(sprintId);
    service.refresh();

    const request = httpMock.expectOne(detailUrl());
    if (status) request.flush({}, { status, statusText: 'Error' });
    else request.flush(payload);
  }

  beforeEach(() => {
    // Both ids start null so the constructor effects settle without firing a request; each test
    // then drives loading explicitly and knows exactly when the HTTP call happens.
    selectedRepoId   = signal<string | null>(null);
    selectedSprintId = signal<string | null>(null);
    sprints          = signal<SprintApiDto[]>([]);
    selectionLoading = signal(false);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        SprintPlanningService,
        { provide: RepositoryContextService, useValue: { selectedRepoId } },
        {
          provide: SprintSelectionService,
          useValue: {
            sprints, selectedSprintId, loading: selectionLoading,
            addSprint: jasmine.createSpy('addSprint'),
            removeSprint: jasmine.createSpy('removeSprint'),
            patchSprint: jasmine.createSpy('patchSprint'),
          },
        },
        {
          provide: PrivilegeService,
          useValue: {
            canManageSprints:  signal(true),
            canManageCapacity: signal(true),
            canEditWorkItem:   signal(true),
          },
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      ],
    });

    service  = TestBed.inject(SprintPlanningService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // ── Team load roll-up ───────────────────────────────────────────────────

  describe('totalCapacity and totalWorkload', () => {
    it('sum across every member', () => {
      load(detail({
        memberLoads: [
          memberLoad({ userId: 'u1', capacity: 80, workload: 40 }),
          memberLoad({ userId: 'u2', capacity: 40, workload: 30 }),
        ],
      }));

      expect(service.totalCapacity()).toBe(120);
      expect(service.totalWorkload()).toBe(70);
    });

    it('are zero when the sprint has no members', () => {
      load(detail({ memberLoads: [] }));

      expect(service.totalCapacity()).toBe(0);
      expect(service.totalWorkload()).toBe(0);
    });

    it('are zero before any detail has loaded', () => {
      expect(service.totalCapacity()).toBe(0);
      expect(service.totalWorkload()).toBe(0);
    });
  });

  describe('teamLoadPercent', () => {
    it('is workload over capacity, rounded', () => {
      load(detail({ memberLoads: [memberLoad({ capacity: 80, workload: 30 })] }));

      // 30/80 = 37.5% → 38
      expect(service.teamLoadPercent()).toBe(38);
    });

    it('is 100 when the team is booked exactly to capacity', () => {
      load(detail({ memberLoads: [memberLoad({ capacity: 80, workload: 80 })] }));

      expect(service.teamLoadPercent()).toBe(100);
    });

    it('goes above 100 when the team is over-booked', () => {
      load(detail({ memberLoads: [memberLoad({ capacity: 80, workload: 100 })] }));

      expect(service.teamLoadPercent()).toBe(125);
    });

    it('reports the 999 sentinel when there is work but no capacity at all', () => {
      // Nobody has been given hours yet, but tasks are already assigned. Dividing by zero would
      // give Infinity and render as a broken bar, so the service substitutes a sentinel the UI
      // caps for display. The backend uses the same 999 — see GetSprintDetailQueryHandlerTests.
      load(detail({ memberLoads: [memberLoad({ capacity: 0, workload: 20 })] }));

      expect(service.teamLoadPercent()).toBe(999);
    });

    it('is 0 when there is neither capacity nor work', () => {
      // An empty sprint is not overloaded — it is simply empty.
      load(detail({ memberLoads: [memberLoad({ capacity: 0, workload: 0 })] }));

      expect(service.teamLoadPercent()).toBe(0);
    });
  });

  describe('teamLoadState', () => {
    /** Loads a sprint whose single member produces the given percentage. */
    function atPercent(workload: number, capacity = 100): void {
      load(detail({ memberLoads: [memberLoad({ capacity, workload })] }));
    }

    it('is safe up to and including 100%', () => {
      atPercent(100);

      expect(service.teamLoadState()).toBe('safe');
    });

    it('is warning just above 100%', () => {
      atPercent(101);

      expect(service.teamLoadState()).toBe('warning');
    });

    it('is still warning at exactly 120%', () => {
      // The boundary is strictly greater than 120, so 120 itself is not yet overloaded.
      atPercent(120);

      expect(service.teamLoadState()).toBe('warning');
    });

    it('is overloaded just above 120%', () => {
      atPercent(121);

      expect(service.teamLoadState()).toBe('overloaded');
    });

    it('is safe for an empty sprint', () => {
      load(detail({ memberLoads: [] }));

      expect(service.teamLoadState()).toBe('safe');
    });

    it('is overloaded when capacity is zero but work exists', () => {
      // The 999 sentinel has to land in the overloaded band, or a sprint with unassigned hours
      // would look healthy.
      atPercent(20, 0);

      expect(service.teamLoadState()).toBe('overloaded');
    });
  });

  describe('cappedPercent', () => {
    it('caps the bar width so the 999 sentinel does not overflow the layout', () => {
      expect(service.cappedPercent(999)).toBe(140);
    });

    it('clamps a negative value to zero', () => {
      expect(service.cappedPercent(-10)).toBe(0);
    });

    it('leaves an ordinary percentage alone', () => {
      expect(service.cappedPercent(85)).toBe(85);
    });
  });

  // ── Working days ────────────────────────────────────────────────────────

  describe('effectiveWorkingDays', () => {
    it('counts weekdays across the sprint range', () => {
      // 1–12 June 2026 is Mon–Fri twice over.
      load(detail());

      expect(service.effectiveWorkingDays()).toBe(10);
    });

    it('excludes weekends rather than counting calendar days', () => {
      // A full seven-day week contains five working days.
      load(detail({ startDate: '2026-06-01', endDate: '2026-06-07' }));

      expect(service.effectiveWorkingDays()).toBe(5);
    });

    it('subtracts a team-wide day off', () => {
      load(detail({ daysOff: [dayOff({ userId: null, date: '2026-06-03' })] }));

      expect(service.effectiveWorkingDays()).toBe(9);
    });

    it('ignores a personal day off', () => {
      // A personal absence reduces one member's capacity, not the sprint's length. Deducting it
      // here would shorten the sprint for the whole team because one person took a day.
      load(detail({ daysOff: [dayOff({ userId: 'user-1', date: '2026-06-03' })] }));

      expect(service.effectiveWorkingDays()).toBe(10);
    });

    it('ignores a team day off that falls on a weekend', () => {
      // 6 June 2026 is a Saturday, which was never a working day to begin with.
      load(detail({ daysOff: [dayOff({ userId: null, date: '2026-06-06' })] }));

      expect(service.effectiveWorkingDays()).toBe(10);
    });

    it('counts a repeated team day off once', () => {
      // Two rows for the same date — the dates go into a Set precisely so this cannot double-count.
      load(detail({
        daysOff: [
          dayOff({ userId: null, date: '2026-06-03' }),
          dayOff({ userId: null, date: '2026-06-03' }),
        ],
      }));

      expect(service.effectiveWorkingDays()).toBe(9);
    });

    it('is zero before any detail has loaded', () => {
      expect(service.effectiveWorkingDays()).toBe(0);
    });

    it('is zero for a sprint that spans only a weekend', () => {
      load(detail({ startDate: '2026-06-06', endDate: '2026-06-07' }));

      expect(service.effectiveWorkingDays()).toBe(0);
    });

    it('handles a single-day sprint on a weekday', () => {
      load(detail({ startDate: '2026-06-01', endDate: '2026-06-01' }));

      expect(service.effectiveWorkingDays()).toBe(1);
    });
  });

  describe('workingDays', () => {
    it('reports the server figure untouched, unlike effectiveWorkingDays', () => {
      // Two different numbers with similar names: `workingDays` is what the API said, while
      // `effectiveWorkingDays` deducts team days off. Keeping them distinguishable matters because
      // the UI shows them side by side.
      load(detail({ workingDays: 10, daysOff: [dayOff({ userId: null, date: '2026-06-03' })] }));

      expect(service.workingDays()).toBe(10);
      expect(service.effectiveWorkingDays()).toBe(9);
    });
  });

  // ── Days off split ──────────────────────────────────────────────────────

  describe('the days-off split', () => {
    beforeEach(() => {
      load(detail({
        daysOff: [
          dayOff({ userId: null,     date: '2026-06-03' }),
          dayOff({ userId: 'user-1', date: '2026-06-04' }),
          dayOff({ userId: 'user-2', date: '2026-06-05' }),
        ],
      }));
    });

    it('puts rows with a user into personalDaysOff', () => {
      expect(service.personalDaysOff().map(d => d.userId)).toEqual(['user-1', 'user-2']);
    });

    it('puts rows without a user into teamDaysOff', () => {
      expect(service.teamDaysOff().length).toBe(1);
      expect(service.teamDaysOff()[0].userId).toBeNull();
    });

    it('accounts for every row exactly once between the two', () => {
      expect(service.personalDaysOff().length + service.teamDaysOff().length)
        .toBe(service.daysOff().length);
    });
  });

  // ── memberLoads mapping ─────────────────────────────────────────────────

  describe('memberLoads', () => {
    it('carries the server-computed figures through unchanged', () => {
      load(detail({
        memberLoads: [memberLoad({
          capacity: 72, workload: 90, loadPercent: 125, loadState: 'overloaded',
        })],
      }));

      const member = service.memberLoads()[0];
      expect(member.capacity).toBe(72);
      expect(member.workload).toBe(90);
      expect(member.loadPercent).toBe(125);
      expect(member.state).toBe('overloaded');
    });

    it('joins the role from the capacity member record', () => {
      load(detail({
        memberLoads:     [memberLoad({ userId: 'user-1' })],
        capacityMembers: [{
          id: 'cap-1', sprintId, userId: 'user-1', userName: 'Nam', userAvatar: 'bg-sky-600',
          role: 'Tester', hoursPerDay: 8, overtimeHoursPerDay: 0,
        }],
      }));

      expect(service.memberLoads()[0].role).toBe('Tester');
    });

    it('falls back to Developer when there is no matching capacity row', () => {
      // A member can appear in the load list without a capacity record — the UI still needs a
      // label for the row rather than an empty cell.
      load(detail({ memberLoads: [memberLoad({ userId: 'user-9' })], capacityMembers: [] }));

      expect(service.memberLoads()[0].role).toBe('Developer');
    });

    it('is empty before any detail has loaded', () => {
      expect(service.memberLoads()).toEqual([]);
    });
  });

  // ── Task flattening ─────────────────────────────────────────────────────

  describe('sprintStories and sprintTaskRows', () => {
    it('separate user stories from everything else', () => {
      load(detail({
        tasks: [
          task({ id: 's1', type: SprintTaskApiType.UserStory, subTasks: [
            task({ id: 't1', type: SprintTaskApiType.Task, parentId: 's1' }),
            task({ id: 'b1', type: SprintTaskApiType.Bug,  parentId: 's1' }),
          ] }),
        ],
      }));

      expect(service.sprintStories().map(t => t.id)).toEqual(['s1']);
      expect(service.sprintTaskRows().map(t => t.id)).toEqual(['t1', 'b1']);
    });

    it('flattens nested sub-tasks at any depth', () => {
      // A sub-task can itself have children; a flatten that only went one level deep would drop
      // them from the board without any visible error.
      load(detail({
        tasks: [
          task({ id: 's1', type: SprintTaskApiType.UserStory, subTasks: [
            task({ id: 't1', parentId: 's1', subTasks: [
              task({ id: 't2', parentId: 't1' }),
            ] }),
          ] }),
        ],
      }));

      expect(service.sprintTaskRows().map(t => t.id)).toEqual(['t1', 't2']);
    });

    it('are empty for a sprint with no tasks', () => {
      load(detail({ tasks: [] }));

      expect(service.sprintStories()).toEqual([]);
      expect(service.sprintTaskRows()).toEqual([]);
    });
  });

  // ── Loading and error state ─────────────────────────────────────────────

  describe('loading', () => {
    it('is true while the shared sprint list is loading, even with no detail request in flight', () => {
      // The page shows one spinner for both. Reporting only its own request would flash the empty
      // state while the sprint dropdown was still filling.
      selectionLoading.set(true);

      expect(service.loading()).toBeTrue();
    });

    it('is false once both have settled', () => {
      load(detail());

      expect(service.loading()).toBeFalse();
    });
  });

  describe('when the detail request fails', () => {
    it('surfaces an error message and stops loading', () => {
      load(null, 500);

      expect(service.error()).toBe('Failed to load sprint detail.');
      expect(service.loading()).toBeFalse();
    });

    it('keeps the figures from the last good load', () => {
      // Deliberate: the page renders the error banner *above* the capacity panel rather than
      // instead of it, so clearing the detail here would blank the numbers a user was reading and
      // replace them with zeros — which look like real data. Stale figures plus a visible error
      // beat a screen of confident zeros.
      load(detail({ memberLoads: [memberLoad({ capacity: 80, workload: 40 })] }));
      expect(service.totalCapacity()).toBe(80);

      load(null, 500);

      expect(service.error()).toBe('Failed to load sprint detail.');
      expect(service.totalCapacity()).toBe(80);
    });
  });

  // ── Sprint selection ────────────────────────────────────────────────────

  describe('selectedSprint', () => {
    const first:  SprintApiDto = { id: 'sprint-1', name: 'Sprint 1', startDate: '2026-06-01', endDate: '2026-06-12', isActive: true } as SprintApiDto;
    const second: SprintApiDto = { id: 'sprint-2', name: 'Sprint 2', startDate: '2026-06-15', endDate: '2026-06-26', isActive: false } as SprintApiDto;

    it('is the sprint whose id is selected', () => {
      sprints.set([first, second]);
      selectedSprintId.set('sprint-2');

      expect(service.selectedSprint()?.id).toBe('sprint-2');
    });

    it('falls back to the first sprint when the selected id is unknown', () => {
      // The selection can name a sprint that was deleted in another tab; the page still needs
      // something to render.
      sprints.set([first, second]);
      selectedSprintId.set('sprint-deleted');

      expect(service.selectedSprint()?.id).toBe('sprint-1');
    });

    it('is null when the repository has no sprints', () => {
      sprints.set([]);

      expect(service.selectedSprint()).toBeNull();
    });
  });
});
