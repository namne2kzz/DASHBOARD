import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { signal } from '@angular/core';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

/**
 * Unit tests for the route guard that keeps signed-out visitors out of the app.
 *
 * A `CanActivateFn` reads its dependencies through `inject`, so it has to be called inside an
 * injection context — hence `TestBed.runInInjectionContext`. The real Router is provided (with an
 * empty route table) rather than stubbed, because the guard's return value is a `UrlTree` and
 * building one by hand would test the stub instead of the guard.
 */
describe('authGuard', () => {
  let isAuthenticated: ReturnType<typeof signal<boolean>>;
  let router: Router;

  function configure(authenticated: boolean): void {
    isAuthenticated = signal(authenticated);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated } },
      ],
    });

    router = TestBed.inject(Router);
  }

  /**
   * Builds the route snapshot the guard actually reads: it walks `pathFromRoot` looking for the
   * `orgAlias` parameter, which real routers put on the shell route rather than on the child the
   * guard runs against. Modelling both segments keeps the stub honest about that shape.
   * @param alias Tenant alias on the shell route, or null for a route outside any tenant.
   */
  function routeWithAlias(alias: string | null): ActivatedRouteSnapshot {
    const shell = { paramMap: convertToParamMap(alias ? { orgAlias: alias } : {}) };
    const child = { paramMap: convertToParamMap({}) };

    return { ...child, pathFromRoot: [shell, child] } as unknown as ActivatedRouteSnapshot;
  }

  /**
   * Runs the guard for a URL the user was trying to reach.
   * @param url The attempted URL. @param alias Tenant alias carried by the route, if any.
   */
  function activate(url: string, alias: string | null = null): boolean | UrlTree {
    const state = { url } as RouterStateSnapshot;

    return TestBed.runInInjectionContext(
      () => authGuard(routeWithAlias(alias), state),
    ) as boolean | UrlTree;
  }

  describe('when the user is authenticated', () => {
    beforeEach(() => configure(true));

    it('lets the navigation through', () => {
      expect(activate('/acme/DASH/boards')).toBeTrue();
    });
  });

  describe('when the user is not authenticated', () => {
    beforeEach(() => configure(false));

    it('redirects to the login page', () => {
      const result = activate('/acme/DASH/boards');

      expect(result).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(result as UrlTree)).toContain('/login');
    });

    it('preserves the intended URL as returnUrl', () => {
      const result = activate('/acme/DASH/boards') as UrlTree;

      expect(result.queryParams['returnUrl']).toBe('/acme/DASH/boards');
    });

    it('keeps the query string of the intended URL intact', () => {
      // Deep links carry state in the query string — losing it means the user lands on the page
      // but without the filter or the item they followed the link for.
      const result = activate('/acme/DASH/backlog?state=Ready&type=UserStory') as UrlTree;

      expect(result.queryParams['returnUrl']).toBe('/acme/DASH/backlog?state=Ready&type=UserStory');
    });

    it('returns a redirect rather than false, so the user is not left on a blank route', () => {
      // Returning false cancels the navigation and leaves the router where it was, which on a cold
      // start is nowhere at all.
      expect(activate('/acme/DASH/boards')).not.toBeFalse();
    });

    it('sends a tenant URL to that tenant\'s sign-in page', () => {
      // Resuming at /acme/login skips the "name your organization" step, which the user has
      // already answered by being on a tenant URL in the first place.
      const result = activate('/acme/DASH/boards', 'acme') as UrlTree;

      expect(router.serializeUrl(result)).toContain('/acme/login');
    });

    it('falls back to the generic sign-in page outside any tenant', () => {
      const result = activate('/settings', null) as UrlTree;

      expect(router.serializeUrl(result)).toContain('/login');
      expect(router.serializeUrl(result)).not.toContain('/acme');
    });
  });

  describe('when the signal flips mid-session', () => {
    it('is re-evaluated on the next navigation', () => {
      configure(true);
      expect(activate('/acme/DASH/boards')).toBeTrue();

      // The error interceptor calls logout() on a 401, which lowers this signal.
      isAuthenticated.set(false);

      expect(activate('/acme/DASH/boards')).toBeInstanceOf(UrlTree);
    });
  });
});
