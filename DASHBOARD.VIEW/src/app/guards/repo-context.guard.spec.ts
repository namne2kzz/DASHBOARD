import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  convertToParamMap,
  Router,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { signal } from '@angular/core';
import { Observable, Subject, firstValueFrom, isObservable } from 'rxjs';
import { repoContextGuard, rootRedirectGuard } from './repo-context.guard';
import { RepositoryContextService } from '../services/repository-context.service';
import { AuthService } from '../services/auth.service';
import { RepositoryApiDto } from '../models/repository.model';
import { UserProfile } from '../models/user.model';

/**
 * Unit tests for the guards that resolve `:repoCode` into a selected repository.
 *
 * These two are the only guards in the app with an asynchronous branch, and that branch is the
 * reason the file is worth its length. On a cold start the repository list has not arrived yet, so
 * the guard returns an observable that waits for `ready$` and only then decides. `ready$` is a plain
 * Subject with no replay, so a guard that subscribed a moment too late would hang forever and the
 * user would sit on a blank screen with no error anywhere — the kind of failure that is nearly
 * impossible to diagnose from a bug report.
 *
 * Each test therefore states plainly which of the two paths it is on: the list is already loaded
 * (synchronous return) or it is not (observable return, resolved by emitting on `ready$`).
 */
describe('repo-context guards', () => {
  let repositories: ReturnType<typeof signal<RepositoryApiDto[]>>;
  let selectedRepo: ReturnType<typeof signal<RepositoryApiDto | null>>;
  let currentUser: ReturnType<typeof signal<UserProfile | null>>;
  let ready$: Subject<true>;
  let select: jasmine.Spy;
  let router: Router;

  const dash   = { id: 'repo-1', code: 'DASH' } as RepositoryApiDto;
  const hub    = { id: 'repo-2', code: 'HUB'  } as RepositoryApiDto;

  const profile: UserProfile = {
    userId: 'user-1', email: 'nam@acme.local', name: 'Nam',
    avatarClass: 'bg-sky-600', isGlobalAdmin: false, orgId: 'org-1', orgAlias: 'acme',
  };

  function configure(options: {
    repos?: RepositoryApiDto[];
    selected?: RepositoryApiDto | null;
    user?: UserProfile | null;
  } = {}): void {
    repositories = signal(options.repos ?? []);
    selectedRepo = signal(options.selected ?? null);
    currentUser  = signal(options.user === undefined ? profile : options.user);
    ready$       = new Subject<true>();
    select       = jasmine.createSpy('select');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: RepositoryContextService,
          useValue: {
            repositories,
            selectedRepo,
            select,
            ready$: ready$.asObservable(),
          },
        },
        { provide: AuthService, useValue: { currentUser } },
      ],
    });

    router = TestBed.inject(Router);
  }

  /** Builds the route snapshot, with an optional `orgAlias` on the parent as the guards read it. */
  function route(repoCode: string | null, parentAlias?: string): ActivatedRouteSnapshot {
    return {
      paramMap: convertToParamMap(repoCode === null ? {} : { repoCode }),
      parent: parentAlias === undefined
        ? null
        : { paramMap: convertToParamMap({ orgAlias: parentAlias }) },
    } as ActivatedRouteSnapshot;
  }

  function path(result: unknown): string {
    return router.serializeUrl(result as UrlTree);
  }

  /**
   * Resolves the guard's result whether it came back directly or as an observable.
   *
   * `ready$` is a bare Subject with no replay, so the subscription has to exist before the emission
   * — calling `next` first and awaiting afterwards hangs forever. Callers therefore hand the
   * emission in as a callback, which runs once `firstValueFrom` has subscribed.
   */
  async function settle(
    result: unknown,
    emit: () => void = () => ready$.next(true),
  ): Promise<boolean | UrlTree> {
    if (!isObservable(result)) return result as boolean | UrlTree;

    const settled = firstValueFrom(result as Observable<boolean | UrlTree>);
    emit();
    return settled;
  }

  // ── repoContextGuard, list already loaded ───────────────────────────────

  describe('repoContextGuard when the repository list is already loaded', () => {
    it('selects the repository whose code matches the URL', () => {
      configure({ repos: [dash, hub] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('HUB'), {} as never),
      );

      expect(result).toBeTrue();
      expect(select).toHaveBeenCalledOnceWith('repo-2');
    });

    it('matches the code regardless of case', () => {
      // Repository codes are upper-case by convention but arrive from typed and pasted URLs.
      configure({ repos: [dash, hub] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('dash'), {} as never),
      );

      expect(result).toBeTrue();
      expect(select).toHaveBeenCalledOnceWith('repo-1');
    });

    it('redirects an unknown code to the first accessible repository', () => {
      configure({ repos: [dash, hub] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('NOPE'), {} as never),
      );

      expect(path(result)).toBe('/acme/DASH/boards');
      expect(select).not.toHaveBeenCalled();
    });

    it('redirects to the organization home when the user has no repositories', async () => {
      // This is the no-access overlay: a user who belongs to the org but to none of its projects.
      // An empty list also means the guard takes the asynchronous branch, since it cannot tell
      // "no repositories" from "not loaded yet".
      configure({ repos: [], user: profile });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('DASH'), {} as never),
      );

      expect(path(await settle(result))).toBe('/acme');
    });

    it('builds the redirect from the user own organization alias', () => {
      configure({ repos: [dash], user: { ...profile, orgAlias: 'globex' } });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('NOPE'), {} as never),
      );

      expect(path(result)).toBe('/globex/DASH/boards');
    });

    it('falls back to the parent route alias when the session has none', () => {
      // A legacy session with no orgAlias still has the alias in the URL it came from, so the
      // redirect can be built from the route instead of ending up at `//DASH/boards`.
      configure({ repos: [dash], user: null });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('NOPE', 'fromurl'), {} as never),
      );

      expect(path(result)).toBe('/fromurl/DASH/boards');
    });
  });

  // ── repoContextGuard, list still loading ────────────────────────────────

  describe('repoContextGuard when the repository list has not arrived yet', () => {
    it('waits instead of deciding on an empty list', async () => {
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('DASH'), {} as never),
      );

      expect(isObservable(result)).toBeTrue();

      // The load completes and populates the list before ready$ fires, exactly as the service does.
      const settled = await settle(result, () => {
        repositories.set([dash]);
        ready$.next(true);
      });

      expect(settled).toBeTrue();
      expect(select).toHaveBeenCalledOnceWith('repo-1');
    });

    it('redirects once it knows the code does not exist', async () => {
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('NOPE'), {} as never),
      );

      const settled = await settle(result, () => {
        repositories.set([dash]);
        ready$.next(true);
      });

      expect(path(settled)).toBe('/acme/DASH/boards');
    });

    it('does not resolve before ready$ emits', () => {
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('DASH'), {} as never),
      );

      let resolved = false;
      (result as Observable<unknown>).subscribe(() => (resolved = true));

      expect(resolved).toBeFalse();
    });

    it('takes only the first emission, so a later reload does not re-trigger it', async () => {
      // The service emits on ready$ every time it reloads. A guard that stayed subscribed would
      // try to resolve a navigation that had already completed.
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => repoContextGuard(route('DASH'), {} as never),
      );

      let emissions = 0;
      (result as Observable<unknown>).subscribe(() => emissions++);

      repositories.set([dash]);
      ready$.next(true);
      ready$.next(true);

      expect(emissions).toBe(1);
    });
  });

  // ── rootRedirectGuard ───────────────────────────────────────────────────

  describe('rootRedirectGuard when the repository list is already loaded', () => {
    it('sends the user to the selected repository board', () => {
      configure({ repos: [dash, hub], selected: hub });

      const result = TestBed.runInInjectionContext(
        () => rootRedirectGuard(route(null), {} as never),
      );

      expect(path(result)).toBe('/acme/HUB/boards');
    });

    it('falls back to the first repository when none is selected', () => {
      configure({ repos: [dash, hub], selected: null });

      const result = TestBed.runInInjectionContext(
        () => rootRedirectGuard(route(null), {} as never),
      );

      expect(path(result)).toBe('/acme/DASH/boards');
    });

    it('stays on the organization home when the user has no repositories', async () => {
      // Returning true leaves the no-access overlay rendered rather than bouncing the user around.
      configure({ repos: [], selected: null });

      const result = TestBed.runInInjectionContext(
        () => rootRedirectGuard(route(null), {} as never),
      );

      expect(await settle(result)).toBeTrue();
    });
  });

  describe('rootRedirectGuard when the repository list has not arrived yet', () => {
    it('waits for the list before redirecting', async () => {
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => rootRedirectGuard(route(null), {} as never),
      );

      expect(isObservable(result)).toBeTrue();

      const settled = await settle(result, () => {
        repositories.set([dash]);
        ready$.next(true);
      });

      expect(path(settled)).toBe('/acme/DASH/boards');
    });

    it('prefers the selected repository over the first one once both are known', async () => {
      configure({ repos: [] });

      const result = TestBed.runInInjectionContext(
        () => rootRedirectGuard(route(null), {} as never),
      );

      const settled = await settle(result, () => {
        repositories.set([dash, hub]);
        selectedRepo.set(hub);
        ready$.next(true);
      });

      expect(path(settled)).toBe('/acme/HUB/boards');
    });
  });
});
