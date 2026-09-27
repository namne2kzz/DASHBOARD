import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';

/**
 * Unit tests for the global HTTP error interceptor.
 *
 * This file is the regression guard for BUG-001. The interceptor used to navigate to `/login`
 * unconditionally on a 401, passing the current URL as `returnUrl`. When the 401 arrived while the
 * user was already sitting on the login page — which happens on every failed sign-in — the
 * `returnUrl` it captured was the login URL itself, complete with the previous `returnUrl` nested
 * inside it. Each further attempt nested it again, so the query string grew exponentially and the
 * eventual redirect target was `/login`, leaving the user stuck on the page they were trying to
 * leave. The `startsWith('/login')` check is the fix, and the tests below pin both halves of it.
 */
describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: jasmine.SpyObj<Pick<AuthService, 'logout'>>;
  let router: jasmine.SpyObj<Pick<Router, 'navigate'>> & { url: string };

  function configure(currentUrl: string): void {
    auth = jasmine.createSpyObj<Pick<AuthService, 'logout'>>('AuthService', ['logout']);

    // Router.url is a getter on the real class, so the stub carries it as a plain property that
    // each test sets to the route the user is supposedly on.
    router = Object.assign(
      jasmine.createSpyObj<Pick<Router, 'navigate'>>('Router', ['navigate']),
      { url: currentUrl },
    );

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });

    http     = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Fires a request and fails it with the given status. */
  function failWith(status: number, url = '/api/v1/users'): { error?: { status: number } } {
    const captured: { error?: { status: number } } = {};

    http.get(url).subscribe({
      next:  () => fail('the request was expected to fail'),
      error: err => (captured.error = err),
    });

    httpMock.expectOne(url).flush({}, { status, statusText: 'Error' });
    return captured;
  }

  afterEach(() => httpMock.verify());

  // ── 401 from inside the app ─────────────────────────────────────────────

  describe('on 401 while on an app route', () => {
    beforeEach(() => configure('/acme/DASH/boards'));

    it('logs the user out', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(auth.logout).toHaveBeenCalledTimes(1);
    });

    it('redirects to /login carrying the route the user was on', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).toHaveBeenCalledOnceWith(
        ['/login'],
        { queryParams: { returnUrl: '/acme/DASH/boards' } },
      );
    });

    it('still propagates the error to the caller', () => {
      // The interceptor handles the session, not the request. A component that needs to show its
      // own message must still see the failure.
      const captured = failWith(HttpStatusCode.Unauthorized);

      expect(captured.error?.status).toBe(HttpStatusCode.Unauthorized);
    });
  });

  // ── BUG-001: 401 while already on the login page ────────────────────────

  describe('on 401 while already on /login', () => {
    beforeEach(() => configure('/login'));

    it('does not navigate at all', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('still logs out, so a stale token cannot survive a failed sign-in', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(auth.logout).toHaveBeenCalledTimes(1);
    });
  });

  describe('on 401 while on /login with a returnUrl already in the query string', () => {
    // This is the exact shape that used to compound. The guard must match on the path, so a
    // check written against the full URL string would fail here.
    beforeEach(() => configure('/login?returnUrl=%2Facme%2FDASH%2Fboards'));

    it('does not nest the returnUrl any further', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('repeated failed sign-in attempts', () => {
    beforeEach(() => configure('/login'));

    it('never navigates, however many times the credentials are rejected', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/login');
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/login');
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/login');

      expect(router.navigate).not.toHaveBeenCalled();
      expect(auth.logout).toHaveBeenCalledTimes(3);
    });
  });

  // ── A route that merely starts with the same letters ────────────────────

  describe('on 401 from a route whose path begins with "login"', () => {
    // startsWith('/login') also matches '/login-help', which is not the login page. Pinning the
    // current behaviour so a future rename of that route is a deliberate decision rather than a
    // surprise: today such a route would be treated as the login page and skip the redirect.
    beforeEach(() => configure('/login-help'));

    it('is treated as the login page and skips the redirect', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  // ── Statuses the interceptor must leave alone ───────────────────────────

  [
    { status: HttpStatusCode.Forbidden,           label: '403 (authenticated but not allowed)' },
    { status: HttpStatusCode.NotFound,            label: '404' },
    { status: HttpStatusCode.BadRequest,          label: '400' },
    { status: HttpStatusCode.InternalServerError, label: '500' },
  ].forEach(({ status, label }) => {
    describe(`on ${label}`, () => {
      beforeEach(() => configure('/acme/DASH/boards'));

      it('neither logs out nor redirects', () => {
        failWith(status);

        expect(auth.logout).not.toHaveBeenCalled();
        expect(router.navigate).not.toHaveBeenCalled();
      });

      it('propagates the error unchanged', () => {
        const captured = failWith(status);

        expect(captured.error?.status).toBe(status);
      });
    });
  });

  // 403 deserves its own note: the API was changed so that a missing privilege answers 403 rather
  // than 401 precisely so this interceptor would stop logging such users out. If 403 ever starts
  // triggering logout again, a user who merely opened a page they lack rights for gets thrown out
  // of the app.
  describe('on 403 specifically', () => {
    beforeEach(() => configure('/acme/DASH/settings/users'));

    it('keeps the session, because the credentials are valid', () => {
      failWith(HttpStatusCode.Forbidden);

      expect(auth.logout).not.toHaveBeenCalled();
    });
  });

  // ── Successful responses ────────────────────────────────────────────────

  describe('on a successful response', () => {
    beforeEach(() => configure('/acme/DASH/boards'));

    it('passes the body through and touches nothing', () => {
      let body: unknown;

      http.get('/api/v1/users').subscribe(res => (body = res));
      httpMock.expectOne('/api/v1/users').flush([{ id: '1' }]);

      expect(body).toEqual([{ id: '1' }]);
      expect(auth.logout).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
