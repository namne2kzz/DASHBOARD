import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { SprintSelectionService } from './sprint-selection.service';
import { RepositoryContextService } from './repository-context.service';
import { environment } from '../../environments/environment';
import { SprintApiDto } from '../models/sprint-planning-api.model';

/**
 * Unit tests for SprintSelectionService — the single answer to "which sprint am I looking at".
 *
 * The Board and Sprint Planning pages both read this, so a wrong answer here shows up as two pages
 * disagreeing with each other, which is reported as a bug in whichever page the user noticed second.
 *
 * Two behaviours carry the risk. `pickDefaultSprint` has three tiers — the sprint containing today,
 * else the nearest future one, else the most recent past one — and the tiers are only distinguishable
 * with data on both sides of today, so the fixtures below are built relative to the current date
 * rather than hard-coded. And the persisted choice is keyed per repository: a key that was not
 * scoped would carry a sprint id across a repository switch, selecting a sprint that does not exist
 * in the repository now on screen.
 */
describe('SprintSelectionService', () => {
  let service: SprintSelectionService;
  let httpMock: HttpTestingController;
  let selectedRepoId: ReturnType<typeof signal<string | null>>;

  const repoId = 'repo-1';

  const sprintsUrl = (repo = repoId) =>
    `${environment.apiBaseUrl}/repositories/${repo}/sprints`;

  const storageKey = (repo = repoId) => `selectedSprintId:${repo}`;

  /** A date offset from today, as the ISO day string the API uses. */
  function day(offset: number): string {
    const d = new Date();
    d.setDate(d.getDate() + offset);
    return d.toISOString().slice(0, 10);
  }

  function sprint(id: string, startOffset: number, endOffset: number): SprintApiDto {
    return {
      id, name: `Sprint ${id}`,
      startDate: day(startOffset), endDate: day(endOffset),
      isActive: false,
    } as SprintApiDto;
  }

  /** The sprint containing today, one entirely in the future, and two in the past. */
  const current = sprint('current', -2, 11);
  const future  = sprint('future',  14, 27);
  const farFuture = sprint('far-future', 30, 43);
  const recentPast = sprint('recent-past', -30, -17);
  const oldPast    = sprint('old-past',    -60, -47);

  /** Selects the repository and flushes the sprint request the constructor effect fires. */
  function load(sprints: SprintApiDto[], options: { repo?: string; fail?: boolean } = {}): void {
    const repo = options.repo ?? repoId;
    selectedRepoId.set(repo);
    TestBed.flushEffects();

    const request = httpMock.expectOne(sprintsUrl(repo));
    if (options.fail) request.flush({}, { status: 500, statusText: 'Error' });
    else request.flush(sprints);
  }

  beforeEach(() => {
    localStorage.clear();
    selectedRepoId = signal<string | null>(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        SprintSelectionService,
        { provide: RepositoryContextService, useValue: { selectedRepoId } },
      ],
    });

    service  = TestBed.inject(SprintSelectionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  // ── Loading ─────────────────────────────────────────────────────────────

  describe('loading', () => {
    it('fetches the sprints once a repository is selected', () => {
      load([current]);

      expect(service.sprints().map(s => s.id)).toEqual(['current']);
      expect(service.loading()).toBeFalse();
    });

    it('requests nothing until a repository is selected', () => {
      expect(service.sprints()).toEqual([]);
    });

    it('surfaces an error message when the request fails', () => {
      load([], { fail: true });

      expect(service.error()).toBe('Failed to load sprints');
      expect(service.loading()).toBeFalse();
    });

    it('clears the previous selection when the repository changes', () => {
      load([current]);
      expect(service.selectedSprintId()).toBe('current');

      load([future], { repo: 'repo-2' });

      expect(service.selectedSprintId()).toBe('future');
    });

    it('empties everything when the repository is deselected', () => {
      load([current]);

      selectedRepoId.set(null);
      TestBed.flushEffects();

      expect(service.sprints()).toEqual([]);
      expect(service.selectedSprintId()).toBeNull();
    });
  });

  // ── Default selection ───────────────────────────────────────────────────

  describe('the default selection', () => {
    it('prefers the sprint that contains today', () => {
      load([recentPast, future, current]);

      expect(service.selectedSprintId()).toBe('current');
    });

    it('falls back to the nearest future sprint when none is running', () => {
      load([recentPast, farFuture, future]);

      expect(service.selectedSprintId()).toBe('future');
    });

    it('falls back to the most recent past sprint when there is no future one', () => {
      // End of a project: everything is finished, so the useful view is the one just closed
      // rather than the oldest in the list.
      load([oldPast, recentPast]);

      expect(service.selectedSprintId()).toBe('recent-past');
    });

    it('selects nothing when the repository has no sprints', () => {
      load([]);

      expect(service.selectedSprintId()).toBeNull();
      expect(service.selectedSprint()).toBeNull();
    });

    it('treats a sprint starting today as the current one', () => {
      // The comparison is inclusive at both ends, so day zero counts.
      load([sprint('starts-today', 0, 13)]);

      expect(service.selectedSprintId()).toBe('starts-today');
    });

    it('treats a sprint ending today as the current one', () => {
      load([sprint('ends-today', -13, 0)]);

      expect(service.selectedSprintId()).toBe('ends-today');
    });
  });

  // ── The persisted choice ────────────────────────────────────────────────

  describe('the persisted choice', () => {
    it('restores a previously selected sprint instead of the default', () => {
      localStorage.setItem(storageKey(), 'recent-past');

      load([recentPast, current]);

      expect(service.selectedSprintId()).toBe('recent-past');
    });

    it('ignores a stored id that is no longer in the list', () => {
      // The sprint was deleted in another session; falling through to the default is the only
      // sensible option, and leaving the dead id selected would render an empty board.
      localStorage.setItem(storageKey(), 'sprint-deleted');

      load([current, future]);

      expect(service.selectedSprintId()).toBe('current');
    });

    it('is written when a sprint is selected', () => {
      load([current, future]);

      service.selectSprint('future');

      expect(localStorage.getItem(storageKey())).toBe('future');
    });

    it('is scoped per repository', () => {
      // The whole point of the key prefix: repo-2 must not inherit repo-1's choice, because the
      // id means nothing there.
      load([current, future]);
      service.selectSprint('future');

      load([recentPast], { repo: 'repo-2' });

      expect(localStorage.getItem(storageKey('repo-1'))).toBe('future');
      expect(localStorage.getItem(storageKey('repo-2'))).toBeNull();
      expect(service.selectedSprintId()).toBe('recent-past');
    });

    it('is restored again when returning to the first repository', () => {
      load([current, future]);
      service.selectSprint('future');

      load([recentPast], { repo: 'repo-2' });
      load([current, future], { repo: repoId });

      expect(service.selectedSprintId()).toBe('future');
    });
  });

  // ── selectedSprint ──────────────────────────────────────────────────────

  describe('selectedSprint', () => {
    it('resolves the full sprint for the selected id', () => {
      load([current, future]);

      service.selectSprint('future');

      expect(service.selectedSprint()?.name).toBe('Sprint future');
    });

    it('is null when the selected id is not in the list', () => {
      load([current]);

      service.selectSprint('nonexistent');

      expect(service.selectedSprint()).toBeNull();
    });
  });

  // ── Mutating the list ───────────────────────────────────────────────────

  describe('addSprint', () => {
    it('appends the sprint and selects it', () => {
      load([current]);

      service.addSprint(future);

      expect(service.sprints().map(s => s.id)).toEqual(['current', 'future']);
      expect(service.selectedSprintId()).toBe('future');
    });

    it('persists the new selection', () => {
      // Creating a sprint and navigating away should not lose it.
      load([current]);

      service.addSprint(future);

      expect(localStorage.getItem(storageKey())).toBe('future');
    });
  });

  describe('patchSprint', () => {
    it('merges the patch into the matching sprint', () => {
      load([current, future]);

      service.patchSprint('future', { name: 'Renamed' });

      expect(service.sprints().find(s => s.id === 'future')?.name).toBe('Renamed');
    });

    it('leaves the other sprints untouched', () => {
      load([current, future]);

      service.patchSprint('future', { name: 'Renamed' });

      expect(service.sprints().find(s => s.id === 'current')?.name).toBe('Sprint current');
    });

    it('keeps the fields the patch does not mention', () => {
      load([current]);

      service.patchSprint('current', { name: 'Renamed' });

      const patched = service.sprints()[0];
      expect(patched.startDate).toBe(current.startDate);
      expect(patched.endDate).toBe(current.endDate);
    });

    it('does nothing for an unknown id', () => {
      load([current]);

      service.patchSprint('nonexistent', { name: 'Renamed' });

      expect(service.sprints().map(s => s.name)).toEqual(['Sprint current']);
    });
  });

  describe('removeSprint', () => {
    it('drops it from the list', () => {
      load([current, future]);

      service.removeSprint('future');

      expect(service.sprints().map(s => s.id)).toEqual(['current']);
    });

    it('picks a new default when the deleted sprint was the selected one', () => {
      // Otherwise the pages would be pointing at a sprint that no longer exists.
      load([current, future]);
      service.selectSprint('future');

      service.removeSprint('future');

      expect(service.selectedSprintId()).toBe('current');
    });

    it('leaves the selection alone when a different sprint is deleted', () => {
      load([current, future]);
      service.selectSprint('future');

      service.removeSprint('current');

      expect(service.selectedSprintId()).toBe('future');
    });

    it('clears the selection when the last sprint is deleted', () => {
      load([current]);

      service.removeSprint('current');

      expect(service.sprints()).toEqual([]);
      expect(service.selectedSprintId()).toBeNull();
    });
  });
});
