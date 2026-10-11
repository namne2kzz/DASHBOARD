import { HttpErrorResponse } from '@angular/common/http';

/** Shown on every page when the API cannot be reached (server stopped, network down, CORS). */
export const SERVER_UNREACHABLE_MESSAGE = "Can't reach the server. Check that the API is running, then try again.";

/**
 * Turns a failed page-load request into the one message every page shows for it, so the same
 * failure reads the same everywhere — and never as raw "Http failure response … 0 Unknown Error".
 * @param err The error emitted by HttpClient (anything else falls back).
 * @param fallback The page's own message for other failures, e.g. "Failed to load sprint detail.".
 * @returns A user-facing sentence.
 */
export function describeLoadError(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;
  if (err.status === 0) return SERVER_UNREACHABLE_MESSAGE;
  if (err.status === 401 || err.status === 403) return "You don't have permission to view this.";
  if (err.status === 404) return 'This item no longer exists or was moved.';
  return fallback;
}
