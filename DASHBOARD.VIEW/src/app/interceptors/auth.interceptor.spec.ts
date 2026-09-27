import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

/**
 * The interceptor is tested through a real HttpClient rather than by calling it directly, because
 * what matters is the request the backend would actually receive — a `req.clone()` that dropped the
 * body or the params would still look correct in a hand-rolled `next` spy.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let token: string | null;

  function configure(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        // Only getToken() is reachable from the interceptor, so the rest of AuthService — which
        // would pull in storage and a preferences HTTP call — stays out of this test.
        { provide: AuthService, useValue: { getToken: () => token } },
      ],
    });

    http     = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  describe('with a stored token', () => {
    beforeEach(() => {
      token = 'signed.jwt.value';
      configure();
    });

    it('attaches it as a bearer Authorization header', () => {
      http.get('/api/v1/users').subscribe();

      const request = httpMock.expectOne('/api/v1/users');
      expect(request.request.headers.get('Authorization')).toBe('Bearer signed.jwt.value');

      request.flush({});
    });

    it('reads the token on every request rather than caching it', () => {
      http.get('/api/v1/first').subscribe();
      httpMock.expectOne('/api/v1/first').flush({});

      // A refresh replaces the token mid-session; the next request must carry the new one.
      token = 'rotated.jwt.value';

      http.get('/api/v1/second').subscribe();
      const second = httpMock.expectOne('/api/v1/second');

      expect(second.request.headers.get('Authorization')).toBe('Bearer rotated.jwt.value');
      second.flush({});
    });

    it('leaves the body, method and params untouched', () => {
      // The interceptor clones the request to add the header. A clone that lost the payload would
      // send an empty POST, which the header assertions above would not notice.
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
      expect(request.request.headers.get('Authorization')).toBe('Bearer signed.jwt.value');

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
  });

  describe('without a stored token', () => {
    beforeEach(() => {
      token = null;
      configure();
    });

    it('sends the request with no Authorization header', () => {
      http.get('/api/v1/auth/login').subscribe();

      const request = httpMock.expectOne('/api/v1/auth/login');
      expect(request.request.headers.has('Authorization')).toBeFalse();

      request.flush({});
    });

    it('does not send the literal string "Bearer null"', () => {
      // A truthiness check that slipped would produce exactly that, and the server would answer
      // 401 on a call that was meant to be anonymous.
      http.get('/api/v1/auth/login').subscribe();

      const request = httpMock.expectOne('/api/v1/auth/login');
      expect(request.request.headers.get('Authorization')).toBeNull();

      request.flush({});
    });
  });

  describe('with an empty token', () => {
    beforeEach(() => {
      // An empty string is falsy, so cleared storage that returns '' instead of null must also
      // result in an anonymous request.
      token = '';
      configure();
    });

    it('is treated as no token at all', () => {
      http.get('/api/v1/auth/login').subscribe();

      const request = httpMock.expectOne('/api/v1/auth/login');
      expect(request.request.headers.has('Authorization')).toBeFalse();

      request.flush({});
    });
  });
});
