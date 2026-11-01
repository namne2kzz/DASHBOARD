import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { errorInterceptor, resetRefreshState } from './error.interceptor';
import { of, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { backgroundRequest } from '../models/http-context.model';
import { LoginResponse } from '../models/auth.model';

/**
 * Unit tests for the global HTTP error interceptor.
 *
 * This file is the regression guard for BUG-001. The interceptor used to navigate to `/login`
 * unconditionally on a 401, passing the current URL as `returnUrl`. When the 401 arrived while the
 * user was already sitting on the login page — which happens on every failed sign-in — the
 * `returnUrl` it captured was the login URL itself, complete with the previous `returnUrl` nested
 * inside it. Each further attempt nested it again, so the query string grew exponentially and the
 * eventual redirect target was `/login`, leaving the user stuck on the page they were trying to
 * leave. Skipping the redirect on public routes is the fix, and the tests below pin both halves.
 *
 * `/login` was only the first such route. Password-setup, invite-accept and tenant-scoped sign-in
 * are public too, and each was later found to suffer the same bounce, so the check covers the set
 * rather than a single path.
 */
/** The slice of AuthService the interceptor touches. */
type AuthStub = Pick<AuthService, 'logout' | 'refresh' | 'isAuthenticated'>;

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: jasmine.SpyObj<AuthStub>;
  let router: jasmine.SpyObj<Pick<Router, 'navigate'>> & { url: string };

  /**
   * Builds the test module.
   * @param currentUrl The route the user is supposedly on.
   * @param opts.refreshSucceeds Whether the stubbed refresh resolves (default) or fails.
   * @param opts.signedIn Whether a session is believed to exist (default true).
   * @param opts.tokenExpired Whether the access token is already known to be spent (default false).
   */
  function configure(
    currentUrl: string,
    opts: { refreshSucceeds?: boolean; signedIn?: boolean; tokenExpired?: boolean } = {},
  ): void {
    const { refreshSucceeds = true, signedIn = true, tokenExpired = false } = opts;

    // Several specs re-configure inside the test body to vary one option after the enclosing
    // beforeEach has already built (and thereby instantiated) the module. Tearing it down first
    // makes that legal instead of throwing "test module has already been instantiated".
    TestBed.resetTestingModule();
    // The module is going away with its interceptor's shared refresh slot still filled; a stale
    // observable there would be joined by the first 401 of the newly configured module.
    resetRefreshState();

    auth = jasmine.createSpyObj<AuthStub>('AuthService', ['logout', 'refresh', 'isAuthenticated']);

    // A real getter on the service, so the stub carries it as a plain property.
    Object.defineProperty(auth, 'isAccessTokenExpired', { value: tokenExpired, configurable: true });

    // The interceptor only spends a refresh attempt when it believes a session exists, so the
    // signal has to answer before any 401 is raised.
    auth.isAuthenticated.and.returnValue(signedIn);

    // The interceptor only cares whether the refresh resolves, never what it carries, so an empty
    // object stands in for the session payload.
    auth.refresh.and.returnValue(
      refreshSucceeds
        ? of({} as LoginResponse)
        : throwError(() => ({ status: HttpStatusCode.Unauthorized })),
    );

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

  /**
   * Fires a request and fails it with the given status.
   *
   * On a 401 the interceptor now tries a refresh and, when that succeeds, replays the original
   * request — so a second pending request appears for the same URL. This helper fails that replay
   * too, which is what makes the session actually end and keeps every assertion below about the
   * final outcome rather than the first attempt. `retries` covers the cases that want the replay
   * to succeed instead.
   */
  function failWith(
    status: number,
    url = '/api/v1/users',
    replay: 'fail' | 'succeed' = 'fail',
  ): { error?: { status: number }; body?: unknown } {
    const captured: { error?: { status: number }; body?: unknown } = {};

    http.get(url).subscribe({
      next:  res => (captured.body = res),
      error: err => (captured.error = err),
    });

    httpMock.expectOne(url).flush({}, { status, statusText: 'Error' });

    // Only a 401 on a refreshable endpoint produces a replay; everything else ends here.
    const replayed = httpMock.match(url);
    if (replayed.length > 0) {
      if (replay === 'succeed') replayed[0].flush({ ok: true });
      else replayed[0].flush({}, { status, statusText: 'Error' });
    }

    return captured;
  }

  afterEach(() => {
    httpMock.verify();
    // The shared refresh slot is module state; leaving one behind would leak into the next spec.
    resetRefreshState();
  });

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
    // '/login-help' is a different route that happens to share a prefix. Matching whole path
    // segments keeps it out of the public list, so a 401 there is treated as any other expired
    // session rather than silently skipping the redirect.
    beforeEach(() => configure('/login-help'));

    it('is not mistaken for the login page', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).toHaveBeenCalledOnceWith(
        ['/login'],
        { queryParams: { returnUrl: '/login-help' } },
      );
    });
  });

  // ── Other routes a signed-out visitor is entitled to be on ──────────────

  // These pages exist to create a session, so a 401 raised while one is open is expected. Sending
  // the visitor to /login would interrupt the very flow they are following — the defect that sent
  // somebody opening their password-setup link straight back to sign-in.
  [
    { url: '/set-password#tok3n',                   label: 'the password-setup page' },
    { url: '/invite/accept#tok3n',                  label: 'the invite-accept page' },
    { url: '/acme/login',                           label: 'tenant-scoped sign-in' },
    { url: '/acme/login?returnUrl=%2Facme%2FDASH',  label: 'tenant-scoped sign-in with a returnUrl' },
  ].forEach(({ url, label }) => {
    describe(`on 401 while on ${label}`, () => {
      beforeEach(() => configure(url));

      it('stays put', () => {
        failWith(HttpStatusCode.Unauthorized);

        expect(router.navigate).not.toHaveBeenCalled();
      });
    });
  });

  describe('on 401 from a tenant route that is not sign-in', () => {
    // The tenant-scoped pattern must not swallow the whole tenant: an expired session on a board
    // still belongs at sign-in.
    beforeEach(() => configure('/acme/DASH/boards'));

    it('still redirects', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(router.navigate).toHaveBeenCalledTimes(1);
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

  // ── Refresh-and-retry ───────────────────────────────────────────────────

  // The reason this exists: cookie auth moved the access token out of JS, and with it went the
  // old refresh path. Every 401 then went straight to logout, so one expired access token — or a
  // single stray 401 from a background poll — ended a session whose refresh cookie was still
  // perfectly good. Sessions dropped constantly in development, where the SSE stream reconnects
  // on every hot reload and each reconnect fires a catch-up read.

  describe('on 401 when the refresh succeeds', () => {
    beforeEach(() => configure('/acme/DASH/boards'));

    it('spends one refresh attempt', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/users', 'succeed');

      expect(auth.refresh).toHaveBeenCalledTimes(1);
    });

    it('keeps the session and stays on the page', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/users', 'succeed');

      expect(auth.logout).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('replays the original request and hands the caller the result', () => {
      const captured = failWith(HttpStatusCode.Unauthorized, '/api/v1/users', 'succeed');

      expect(captured.body).toEqual({ ok: true });
      expect(captured.error).toBeUndefined();
    });
  });

  describe('on 401 when the refresh also fails', () => {
    beforeEach(() => configure('/acme/DASH/boards', { refreshSucceeds: false }));

    it('ends the session', () => {
      failWith(HttpStatusCode.Unauthorized);

      expect(auth.logout).toHaveBeenCalledTimes(1);
      expect(router.navigate).toHaveBeenCalledOnceWith(
        ['/login'],
        { queryParams: { returnUrl: '/acme/DASH/boards' } },
      );
    });

    it('reports the original 401 rather than the refresh failure', () => {
      // The caller asked about its own request; the refresh is an implementation detail it has
      // no way to interpret.
      const captured = failWith(HttpStatusCode.Unauthorized);

      expect(captured.error?.status).toBe(HttpStatusCode.Unauthorized);
    });
  });

  describe('several requests hitting 401 at once', () => {
    beforeEach(() => configure('/acme/DASH/boards'));

    it('refreshes once for all of them', () => {
      // A screen opening three panels produces three simultaneous 401s. Because the server
      // rotates the refresh token on every exchange, three separate refreshes would mean the
      // first invalidates the token the other two carry, and both would come back 401 — turning
      // one expired access token into a logout.
      const urls = ['/api/v1/a', '/api/v1/b', '/api/v1/c'];
      urls.forEach(u => http.get(u).subscribe({ next: () => {}, error: () => {} }));
      urls.forEach(u => httpMock.expectOne(u).flush({}, { status: 401, statusText: 'Error' }));

      // Each original request is replayed once the shared refresh resolves.
      urls.forEach(u => httpMock.match(u).forEach(r => r.flush({ ok: true })));

      expect(auth.refresh).toHaveBeenCalledTimes(1);
      expect(auth.logout).not.toHaveBeenCalled();
    });
  });

  describe('on 401 from a credential endpoint', () => {
    // A rejected sign-in is not an expired session: there is nothing to refresh, and the user
    // needs to see that the password was wrong rather than have a refresh attempted behind it.
    ['/api/v1/auth/login', '/api/v1/auth/refresh', '/api/v1/auth/set-password'].forEach(url => {
      it(`never attempts a refresh for ${url}`, () => {
        configure('/login');
        failWith(HttpStatusCode.Unauthorized, url);

        expect(auth.refresh).not.toHaveBeenCalled();
      });
    });
  });

  // ── Renewing before the request goes out ────────────────────────────────

  // Once the expiry is known there is no reason to let the server reject a request that is
  // certain to fail: refreshing first turns three round trips into one and keeps 401s the user
  // did not cause out of the console.

  describe('when the access token is already known to be spent', () => {
    beforeEach(() => configure('/acme/DASH/boards', { tokenExpired: true }));

    it('refreshes before sending, so the request is never rejected', () => {
      let body: unknown;
      http.get('/api/v1/users').subscribe(res => (body = res));

      // No 401 is flushed anywhere here: the only request to reach the server is the replay.
      httpMock.expectOne('/api/v1/users').flush({ ok: true });

      expect(auth.refresh).toHaveBeenCalledTimes(1);
      expect(body).toEqual({ ok: true });
    });

    it('keeps the session when the refresh works', () => {
      http.get('/api/v1/users').subscribe({ next: () => {}, error: () => {} });
      httpMock.expectOne('/api/v1/users').flush({ ok: true });

      expect(auth.logout).not.toHaveBeenCalled();
    });

    it('refreshes once for several requests sent while expired', () => {
      const urls = ['/api/v1/a', '/api/v1/b', '/api/v1/c'];
      urls.forEach(u => http.get(u).subscribe({ next: () => {}, error: () => {} }));
      urls.forEach(u => httpMock.match(u).forEach(r => r.flush({ ok: true })));

      expect(auth.refresh).toHaveBeenCalledTimes(1);
    });

    it('ends the session when the refresh fails', () => {
      configure('/acme/DASH/boards', { tokenExpired: true, refreshSucceeds: false });

      let status: number | undefined;
      http.get('/api/v1/users').subscribe({
        next:  () => fail('the request was expected to fail'),
        error: (e: { status: number }) => (status = e.status),
      });

      // Nothing was ever sent, so the caller still needs an answer shaped like the 401 it would
      // otherwise have received.
      expect(status).toBe(HttpStatusCode.Unauthorized);
      expect(auth.logout).toHaveBeenCalledTimes(1);
    });

    it('does not pre-empt a sign-in, which is how an expired session gets replaced', () => {
      configure('/login', { tokenExpired: true });

      http.post('/api/v1/auth/login', {}).subscribe({ next: () => {}, error: () => {} });
      httpMock.expectOne('/api/v1/auth/login').flush({ ok: true });

      expect(auth.refresh).not.toHaveBeenCalled();
    });

    it('renews for a background request too, so a poll does not fail on a known-dead token', () => {
      let body: unknown;
      http.get('/api/v1/notifications', { context: backgroundRequest() })
        .subscribe(res => (body = res));
      httpMock.expectOne('/api/v1/notifications').flush({ ok: true });

      expect(auth.refresh).toHaveBeenCalledTimes(1);
      expect(body).toEqual({ ok: true });
    });
  });

  describe('when the expiry is unknown', () => {
    // After a reload the token lives only in an HttpOnly cookie, so the expiry cannot be read.
    // Guessing would spend the refresh token on a session that is probably fine; those requests
    // fall back to the 401-and-retry path instead.
    beforeEach(() => configure('/acme/DASH/boards', { tokenExpired: false }));

    it('sends the request as normal', () => {
      http.get('/api/v1/users').subscribe({ next: () => {}, error: () => {} });
      httpMock.expectOne('/api/v1/users').flush({ ok: true });

      expect(auth.refresh).not.toHaveBeenCalled();
    });
  });

  // ── The logout endpoint must be inert ───────────────────────────────────

  describe('on 401 from /auth/logout', () => {
    // Regression guard for a loop introduced while adding refresh-and-retry. logout() posts to
    // this endpoint; when an already-dead session made that post 401, the interceptor ended the
    // session again, which posted again — measured at 464 requests in three seconds against the
    // running app. Nothing here may touch the session: whoever called logout is already tearing
    // it down.
    beforeEach(() => configure('/acme/DASH/boards'));

    it('does not call logout again', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/logout');

      expect(auth.logout).not.toHaveBeenCalled();
    });

    it('does not navigate', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/logout');

      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('does not attempt a refresh', () => {
      failWith(HttpStatusCode.Unauthorized, '/api/v1/auth/logout');

      expect(auth.refresh).not.toHaveBeenCalled();
    });
  });

  describe('on 401 while signed out', () => {
    beforeEach(() => configure('/acme/DASH/boards', { signedIn: false }));

    it('does not bother refreshing', () => {
      // No session means no refresh cookie worth spending a round-trip on.
      failWith(HttpStatusCode.Unauthorized);

      expect(auth.refresh).not.toHaveBeenCalled();
      expect(auth.logout).toHaveBeenCalledTimes(1);
    });
  });

  // ── Background requests ─────────────────────────────────────────────────

  describe('on 401 from a background request whose refresh fails', () => {
    beforeEach(() => configure('/acme/DASH/boards', { refreshSucceeds: false }));

    it('leaves the user where they are', () => {
      // This is the SSE reconnect's catch-up read. Signing the user out here would discard a form
      // they are halfway through because of a request they never made; the next thing they do
      // themselves will find the same dead session and end it then.
      let failed = false;
      http.get('/api/v1/notifications', { context: backgroundRequest() })
        .subscribe({ next: () => {}, error: () => (failed = true) });

      httpMock.expectOne('/api/v1/notifications')
        .flush({}, { status: 401, statusText: 'Error' });
      httpMock.match('/api/v1/notifications')
        .forEach(r => r.flush({}, { status: 401, statusText: 'Error' }));

      expect(failed).toBeTrue();
      expect(auth.logout).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
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
