import { HttpErrorResponse } from '@angular/common/http';
import { describeLoadError, SERVER_UNREACHABLE_MESSAGE } from './http-error.util';

describe('describeLoadError', () => {
  const fallback = 'Failed to load things.';
  const http = (status: number) => new HttpErrorResponse({ status, url: '/api/v1/x' });

  it('reports an unreachable server for status 0 instead of the raw HttpClient message', () => {
    expect(describeLoadError(http(0), fallback)).toBe(SERVER_UNREACHABLE_MESSAGE);
  });

  it('reports missing permission for 401 and 403', () => {
    expect(describeLoadError(http(401), fallback)).toContain('permission');
    expect(describeLoadError(http(403), fallback)).toContain('permission');
  });

  it('reports a missing item for 404', () => {
    expect(describeLoadError(http(404), fallback)).toContain('no longer exists');
  });

  it("keeps the page's own message for server errors and non-HTTP errors", () => {
    expect(describeLoadError(http(500), fallback)).toBe(fallback);
    expect(describeLoadError(new Error('boom'), fallback)).toBe(fallback);
  });
});
