import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../environments/environment';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);
const CSRF_COOKIE = 'XSRF-TOKEN';
const CSRF_HEADER = 'X-XSRF-TOKEN';

/** Reads a non-HttpOnly cookie value by name, or null. */
function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

/**
 * Prepares every API request for cookie-based auth:
 *  - sets withCredentials so the HttpOnly auth cookie is sent (required cross-origin too);
 *  - on mutating methods, echoes the readable XSRF-TOKEN cookie into the X-XSRF-TOKEN header so the
 *    backend's double-submit check passes.
 *
 * We attach the CSRF header manually rather than relying solely on Angular's built-in XSRF support,
 * because that support deliberately does not attach the header to absolute cross-origin URLs — which
 * is exactly the dev setup (SPA on :4200, API on :5152). Doing it here works for both dev and prod.
 * Third-party URLs are left untouched so neither the cookie nor the token leaks to them.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiBaseUrl)) return next(req);

  let headers = req.headers;
  if (!SAFE_METHODS.has(req.method.toUpperCase())) {
    const csrf = readCookie(CSRF_COOKIE);
    if (csrf) headers = headers.set(CSRF_HEADER, csrf);
  }

  return next(req.clone({ withCredentials: true, headers }));
};
