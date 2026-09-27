import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  convertToParamMap,
  Router,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { signal } from '@angular/core';
import { orgContextGuard, orgHomeRedirectGuard } from './org-context.guard';
import { AuthService } from '../services/auth.service';
import { UserProfile } from '../models/user.model';

/**
 * Unit tests for the two guards that keep the `:orgAlias` URL segment honest.
 *
 * Every route in the app is namespaced by the organization alias, so these guards decide where a
 * user lands before any page loads. They are cheap to get subtly wrong — an alias comparison that
 * is case-sensitive, or a redirect that fires for a user who simply is not signed in yet — and the
 * symptom is a redirect loop rather than an error, which makes it hard to trace from a bug report.
 */
describe('org-context guards', () => {
  let currentUser: ReturnType<typeof signal<UserProfile | null>>;
  let router: Router;

  const profile: UserProfile = {
    userId: 'user-1', email: 'nam@acme.local', name: 'Nam',
    avatarClass: 'bg-sky-600', isGlobalAdmin: false, orgId: 'org-1', orgAlias: 'acme',
  };

  function configure(user: UserProfile | null): void {
    currentUser = signal(user);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser } },
      ],
    });

    router = TestBed.inject(Router);
  }

  /** Builds the minimal route snapshot the guard reads, carrying an `orgAlias` param. */
  function routeWithAlias(orgAlias: string | null): ActivatedRouteSnapshot {
    return {
      paramMap: convertToParamMap(orgAlias === null ? {} : { orgAlias }),
    } as ActivatedRouteSnapshot;
  }

  function path(result: boolean | UrlTree): string {
    return router.serializeUrl(result as UrlTree);
  }

  // ── orgHomeRedirectGuard: the bare root ─────────────────────────────────

  describe('orgHomeRedirectGuard', () => {
    it('sends a signed-in user to their own organization home', () => {
      configure(profile);

      const result = TestBed.runInInjectionContext(
        () => orgHomeRedirectGuard(routeWithAlias(null), {} as never),
      );

      expect(path(result as UrlTree)).toBe('/acme');
    });

    it('sends a signed-out visitor to the login page', () => {
      configure(null);

      const result = TestBed.runInInjectionContext(
        () => orgHomeRedirectGuard(routeWithAlias(null), {} as never),
      );

      expect(path(result as UrlTree)).toBe('/login');
    });

    it('treats a session with no alias as signed out', () => {
      // A session stored before organizations existed has no orgAlias. Redirecting such a user to
      // `/undefined` would be a dead end, so they are sent to log in again.
      configure({ ...profile, orgAlias: '' });

      const result = TestBed.runInInjectionContext(
        () => orgHomeRedirectGuard(routeWithAlias(null), {} as never),
      );

      expect(path(result as UrlTree)).toBe('/login');
    });

    it('always redirects — it never allows the bare root to render', () => {
      configure(profile);

      const result = TestBed.runInInjectionContext(
        () => orgHomeRedirectGuard(routeWithAlias(null), {} as never),
      );

      expect(result).toBeInstanceOf(UrlTree);
    });
  });

  // ── orgContextGuard: validating the alias in the URL ────────────────────

  describe('orgContextGuard', () => {
    function activate(alias: string | null): boolean | UrlTree {
      return TestBed.runInInjectionContext(
        () => orgContextGuard(routeWithAlias(alias), {} as never),
      ) as boolean | UrlTree;
    }

    it('allows the alias that matches the signed-in user', () => {
      configure(profile);

      expect(activate('acme')).toBeTrue();
    });

    it('matches the alias regardless of case', () => {
      // Aliases arrive from hand-typed URLs and pasted links, so a case difference must not bounce
      // the user out of a page they are entitled to.
      configure(profile);

      expect(activate('ACME')).toBeTrue();
      expect(activate('Acme')).toBeTrue();
    });

    it('redirects a mismatched alias to the user own organization', () => {
      configure(profile);

      const result = activate('globex');

      expect(path(result)).toBe('/acme');
    });

    it('redirects using the stored casing, not the requested one', () => {
      configure({ ...profile, orgAlias: 'AcmeCorp' });

      const result = activate('globex');

      expect(path(result)).toBe('/AcmeCorp');
    });

    it('allows the navigation when nobody is signed in, leaving it to authGuard', () => {
      // Redirecting here as well would race with authGuard and produce a loop: this guard would
      // send the user to `/`, the root guard would send them to `/login`, and any deep link would
      // be lost on the way. The comment in the guard says so explicitly.
      configure(null);

      expect(activate('acme')).toBeTrue();
    });

    it('allows the navigation for a legacy session with no alias', () => {
      configure({ ...profile, orgAlias: '' });

      expect(activate('acme')).toBeTrue();
    });

    it('redirects when the route carries no alias at all but the user has one', () => {
      // A missing param reads as an empty string, which cannot match a real alias.
      configure(profile);

      const result = activate(null);

      expect(path(result)).toBe('/acme');
    });
  });
});
