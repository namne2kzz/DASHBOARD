import { HttpErrorResponse, HttpInterceptorFn, HttpRequest, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { BACKGROUND_REQUEST } from '../models/http-context.model';
import { AuthService } from '../services/auth.service';

/**
 * Routes a signed-out visitor is entitled to be on. A 401 raised while one of these is open is
 * expected — the visitor has no session yet, and some of these pages exist precisely to create one
 * — so bouncing them to /login would interrupt the very flow they are following.
 *
 * Matched against `Router.url`, which always starts with `/` and may carry a query or fragment.
 */
const PUBLIC_ROUTES = ['/login', '/set-password', '/invite/accept'];

/**
 * Endpoints that must never trigger a refresh attempt.
 *
 * `/auth/refresh` is the refresh call itself — retrying it on its own 401 would recurse. The rest
 * carry their credential in the body and establish a session rather than consuming one, so their
 * 401 means "wrong credentials", not "expired session"; the user must see that error rather than
 * have a refresh attempted behind it.
 */
const NO_REFRESH_PATHS = [
  '/auth/refresh', '/auth/login', '/auth/google-login', '/auth/set-password',
];

/**
 * The logout endpoint, which a 401 must leave entirely alone.
 *
 * `logout()` posts here, so ending the session in reaction to its failure calls `logout()` again,
 * which posts again: an unbounded loop that fires hundreds of requests a second. The session is
 * already being torn down by whoever called logout, so there is nothing left for this interceptor
 * to do.
 */
const LOGOUT_PATH = '/auth/logout';

/** @param url The current `Router.url`. @returns true when it is one of the public routes. */
function isPublicRoute(url: string): boolean {
  // Sign-in also lives at /{orgAlias}/login, so that shape is recognised too.
  if (/^\/[^/]+\/login(\/|\?|#|$)/.test(url)) return true;

  return PUBLIC_ROUTES.some(route => url === route || url.startsWith(`${route}/`)
    || url.startsWith(`${route}?`) || url.startsWith(`${route}#`));
}

/** @param req The outgoing request. @returns Its URL without the query string. */
function requestPath(req: HttpRequest<unknown>): string {
  return req.url.split('?')[0];
}

/** @param req The outgoing request. @returns true when a 401 from it must not be refreshed. */
function skipsRefresh(req: HttpRequest<unknown>): boolean {
  // Matched on a suffix so the api/v{n} prefix and any host do not have to be spelled out.
  const path = requestPath(req);
  return NO_REFRESH_PATHS.some(p => path.endsWith(p));
}

/**
 * The in-flight refresh, shared by every request that hits a 401 while it runs.
 *
 * Module-level rather than per-request: a screen opening eight panels at once produces eight
 * simultaneous 401s, and without a shared observable each would post its own /auth/refresh. Since
 * the server rotates the refresh token on every exchange, the first call would invalidate the token
 * the other seven are carrying, and those would all come back 401 — turning one expired access
 * token into a logout. `shareReplay(1)` makes the later arrivals wait on the first call's result;
 * the field is cleared when it settles so the next expiry starts a fresh attempt.
 */
let refreshInFlight: Observable<unknown> | null = null;

/**
 * Discards any in-flight refresh. For tests only: the shared slot is module state, so without this
 * one spec's refresh would still be sitting there when the next one runs.
 */
export function resetRefreshState(): void {
  refreshInFlight = null;
}

/**
 * Handles HTTP error responses globally.
 *
 * A 401 is treated as an expired access token first, not as a lost session: the interceptor spends
 * one refresh attempt and replays the original request. Only when that refresh fails is the session
 * actually over, and the user is signed out and sent to sign-in. Before this, every 401 logged the
 * user out immediately — so a single expired token, or one stray 401 from a background poll, ended
 * the session even though a valid refresh cookie was sitting right there.
 *
 * 403/5xx pass through untouched: they are answers, not session problems.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  /**
   * Signs the user out and, unless they are on a public page, sends them to sign-in.
   *
   * A background request leaves the session untouched: a poll or a reconnect's catch-up read is
   * not the user's action, and tearing down the page they are working on because of one would
   * discard whatever they had half-finished. The next request they make themselves ends it.
   */
  const endSession = (): void => {
    if (req.context.get(BACKGROUND_REQUEST)) return;

    auth.logout();
    const currentUrl = router.url;
    if (!isPublicRoute(currentUrl)) {
      router.navigate(['/login'], { queryParams: { returnUrl: currentUrl } });
    }
  };

  /** The shared refresh exchange, joined by every caller that needs one while it runs. */
  const refresh = (): Observable<unknown> => {
    if (refreshInFlight) return refreshInFlight;

    const shared = auth.refresh().pipe(
      // Clearing the slot is deferred to a microtask rather than done the instant the exchange
      // settles. The burst this exists to collapse is synchronous — eight panels' 401s all land
      // in one task — so a slot cleared synchronously on completion is already empty when the
      // second 401 of that same burst arrives, and each one starts its own refresh after all.
      // Waiting a microtask keeps the slot occupied for the whole burst while still releasing it
      // long before the next one. Placed before shareReplay so it runs once on the shared source
      // rather than once per subscriber.
      tap({
        complete: () => queueMicrotask(() => { refreshInFlight = null; }),
        error:    () => queueMicrotask(() => { refreshInFlight = null; }),
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    refreshInFlight = shared;
    return shared;
  };

  // Known-expired token: renew before sending rather than letting the request be rejected and
  // replayed. The round trip the server would spend saying "no" is skipped entirely, and nothing
  // the user did shows up in the console as a 401. Credential endpoints are exempt — signing in
  // with an expired session is exactly how a session gets replaced.
  if (auth.isAuthenticated() && auth.isAccessTokenExpired && !skipsRefresh(req)
      && !requestPath(req).endsWith(LOGOUT_PATH)) {
    return refresh().pipe(
      switchMap(() => next(req)),
      catchError(() => {
        // The refresh token is gone too, so there is no session left to send this with.
        endSession();
        return throwError(() => new HttpErrorResponse({
          status: HttpStatusCode.Unauthorized,
          statusText: 'Session expired',
          url: req.url,
        }));
      }),
    );
  }

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== HttpStatusCode.Unauthorized) return throwError(() => err);

      // The logout call itself: react to it in any way and logout() runs again, posting again.
      // Must come before every other branch, since all of them end the session.
      if (requestPath(req).endsWith(LOGOUT_PATH)) return throwError(() => err);

      // Credential endpoints and the refresh call itself: a 401 here is final.
      if (skipsRefresh(req)) {
        endSession();
        return throwError(() => err);
      }

      // Nothing to refresh with when there is no session to begin with — this is a signed-out
      // visitor touching a protected endpoint, so skip the pointless round-trip.
      if (!auth.isAuthenticated()) {
        endSession();
        return throwError(() => err);
      }

      // The token looked valid but the server disagreed — a clock skew, or it was revoked. Same
      // shared exchange as the pre-emptive path above, so a mix of the two still refreshes once.
      return refresh().pipe(
        // The new access cookie is already set by the refresh response, so the replay is the
        // original request unchanged — the browser attaches the fresh cookie itself.
        switchMap(() => next(req)),
        catchError(() => {
          // Refresh failed: the session really is over. Surface the original 401, not the
          // refresh's, since that is what the caller asked about.
          endSession();
          return throwError(() => err);
        }),
      );
    })
  );
};
