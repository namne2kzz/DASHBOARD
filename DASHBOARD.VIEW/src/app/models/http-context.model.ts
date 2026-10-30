import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marks a request as background work the user did not ask for: a poll, a prefetch, a reconnect's
 * catch-up read.
 *
 * The error interceptor still spends a refresh attempt on a 401 from such a request, but when that
 * refresh fails it leaves the session alone instead of signing the user out and navigating away.
 * The reason is whose action it was: a background poll failing must not throw away the form
 * somebody is halfway through typing. The next request the user actually makes will find the same
 * dead session and end it then, with the navigation happening at a moment they can connect to
 * something they did.
 */
export const BACKGROUND_REQUEST = new HttpContextToken<boolean>(() => false);

/** @returns An HttpContext marking the request as background work. */
export function backgroundRequest(): HttpContext {
  return new HttpContext().set(BACKGROUND_REQUEST, true);
}
