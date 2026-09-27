import { HttpInterceptorFn } from '@angular/common/http';

/** Header the backend reads and echoes; must match CorrelationIdMiddleware.HeaderName. */
const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * Tags every outgoing request with a fresh correlation id so the backend — and any service it
 * calls onward — logs that request under one id the user can quote when reporting a problem.
 */
export const correlationIdInterceptor: HttpInterceptorFn = (req, next) => {
  // Per request, not per session: one id per user action is what makes a log search precise.
  return next(req.clone({ setHeaders: { [CORRELATION_HEADER]: newCorrelationId() } }));
};

/** Builds a correlation id, falling back when crypto.randomUUID is unavailable (non-HTTPS origins). */
function newCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
