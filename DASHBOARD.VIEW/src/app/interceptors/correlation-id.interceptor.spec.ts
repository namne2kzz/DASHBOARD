import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { correlationIdInterceptor } from './correlation-id.interceptor';

/**
 * Driven through a real HttpClient rather than a hand-rolled `next` spy, so the assertions describe
 * the request the backend would actually receive — a `req.clone()` that dropped the body or params
 * would still satisfy a header-only check against a fake.
 */
describe('correlationIdInterceptor', () => {
  const HEADER = 'X-Correlation-Id';

  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([correlationIdInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http     = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches a correlation id to the request', () => {
    http.get('/api/v1/users').subscribe();

    const request = httpMock.expectOne('/api/v1/users');
    expect(request.request.headers.get(HEADER)).toBeTruthy();

    request.flush({});
  });

  it('sends a different id per request', () => {
    // One id per user action is the whole point: a session-wide constant would make a log search
    // return every request the user ever made.
    http.get('/api/v1/first').subscribe();
    const first = httpMock.expectOne('/api/v1/first');
    const firstId = first.request.headers.get(HEADER);
    first.flush({});

    http.get('/api/v1/second').subscribe();
    const second = httpMock.expectOne('/api/v1/second');

    expect(second.request.headers.get(HEADER)).not.toBe(firstId);
    second.flush({});
  });

  it('sends an id the backend will accept rather than replace', () => {
    // CorrelationIdMiddleware discards an inbound id longer than 64 chars or containing anything
    // outside [A-Za-z0-9-_:.], so an id this side generates must stay inside that alphabet.
    http.get('/api/v1/users').subscribe();

    const request = httpMock.expectOne('/api/v1/users');
    const id = request.request.headers.get(HEADER)!;

    expect(id.length).toBeLessThanOrEqual(64);
    expect(id).toMatch(/^[A-Za-z0-9\-_:.]+$/);

    request.flush({});
  });

  it('leaves the body, method and params untouched', () => {
    http.post('/api/v1/items', { title: 'Keep me' }, { params: { repoId: 'abc' } }).subscribe();

    const request = httpMock.expectOne(r => r.url === '/api/v1/items');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ title: 'Keep me' });
    expect(request.request.params.get('repoId')).toBe('abc');

    request.flush({});
  });

  it('preserves headers the caller set', () => {
    http.get('/api/v1/export', { headers: { Accept: 'text/csv' } }).subscribe();

    const request = httpMock.expectOne('/api/v1/export');
    expect(request.request.headers.get('Accept')).toBe('text/csv');
    expect(request.request.headers.get(HEADER)).toBeTruthy();

    request.flush('');
  });

  it('does not swallow an error response', () => {
    let status: number | undefined;

    http.get('/api/v1/users').subscribe({ error: err => (status = err.status) });

    httpMock.expectOne('/api/v1/users').flush(
      {},
      { status: HttpStatusCode.InternalServerError, statusText: 'Server Error' },
    );

    expect(status).toBe(HttpStatusCode.InternalServerError);
  });

  describe('when crypto.randomUUID is unavailable', () => {
    let original: typeof crypto.randomUUID;

    beforeEach(() => {
      // Served over plain HTTP, crypto.randomUUID can be missing; the fallback must still produce
      // a usable id instead of throwing and breaking every request in the app.
      original = crypto.randomUUID;
      (crypto as { randomUUID?: unknown }).randomUUID = undefined;
    });

    afterEach(() => {
      (crypto as { randomUUID?: unknown }).randomUUID = original;
    });

    it('still sends a valid id', () => {
      http.get('/api/v1/users').subscribe();

      const request = httpMock.expectOne('/api/v1/users');
      const id = request.request.headers.get(HEADER)!;

      expect(id).toMatch(/^[A-Za-z0-9\-_:.]+$/);
      expect(id.length).toBeGreaterThan(0);

      request.flush({});
    });
  });
});
