import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { environment } from '../../environments/environment';

/**
 * The interceptor is tested through a real HttpClient rather than by calling it directly, because
 * what matters is the request the backend would actually receive — a `req.clone()` that dropped the
 * body or the params would still look correct in a hand-rolled `next` spy.
 *
 * Since the switch to HttpOnly cookie auth, the interceptor no longer attaches a bearer token. Its
 * only job is to mark requests to our own API as credentialed (withCredentials) so the browser
 * sends the auth cookie and Angular attaches the XSRF header; third-party URLs must be left alone
 * so our cookies never leak to them.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  const apiUrl = `${environment.apiBaseUrl}/users`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http     = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  describe('for a request to our own API', () => {
    it('marks the request as credentialed so the auth cookie is sent', () => {
      http.get(apiUrl).subscribe();

      const request = httpMock.expectOne(apiUrl);
      expect(request.request.withCredentials).toBeTrue();

      request.flush({});
    });

    it('leaves the body, method and params untouched', () => {
      http.post(`${environment.apiBaseUrl}/items`, { title: 'Keep me' }, { params: { repoId: 'abc' } }).subscribe();

      const request = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/items`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ title: 'Keep me' });
      expect(request.request.params.get('repoId')).toBe('abc');
      expect(request.request.withCredentials).toBeTrue();

      request.flush({});
    });

    it('preserves headers the caller set', () => {
      http.get(`${environment.apiBaseUrl}/export`, { headers: { Accept: 'text/csv' } }).subscribe();

      const request = httpMock.expectOne(`${environment.apiBaseUrl}/export`);
      expect(request.request.headers.get('Accept')).toBe('text/csv');
      expect(request.request.withCredentials).toBeTrue();

      request.flush('');
    });

    it('does not swallow an error response', () => {
      let status: number | undefined;

      http.get(apiUrl).subscribe({ error: err => (status = err.status) });

      httpMock.expectOne(apiUrl).flush(
        {},
        { status: HttpStatusCode.InternalServerError, statusText: 'Server Error' },
      );

      expect(status).toBe(HttpStatusCode.InternalServerError);
    });
  });

  describe('CSRF double-submit header', () => {
    afterEach(() => {
      // Expire the test cookie so it does not bleed into other specs.
      document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
    });

    it('echoes the XSRF-TOKEN cookie into X-XSRF-TOKEN on a mutating request', () => {
      document.cookie = 'XSRF-TOKEN=csrf-secret-123; path=/';

      http.post(`${environment.apiBaseUrl}/items`, {}).subscribe();

      const request = httpMock.expectOne(`${environment.apiBaseUrl}/items`);
      expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-secret-123');

      request.flush({});
    });

    it('does not attach the CSRF header on a safe (GET) request', () => {
      document.cookie = 'XSRF-TOKEN=csrf-secret-123; path=/';

      http.get(apiUrl).subscribe();

      const request = httpMock.expectOne(apiUrl);
      expect(request.request.headers.has('X-XSRF-TOKEN')).toBeFalse();

      request.flush({});
    });

    it('omits the header when no CSRF cookie is present', () => {
      http.post(`${environment.apiBaseUrl}/items`, {}).subscribe();

      const request = httpMock.expectOne(`${environment.apiBaseUrl}/items`);
      expect(request.request.headers.has('X-XSRF-TOKEN')).toBeFalse();

      request.flush({});
    });
  });

  describe('for a third-party URL', () => {
    it('does not send credentials, so our cookies never leak to it', () => {
      http.get('https://third-party.example.com/data').subscribe();

      const request = httpMock.expectOne('https://third-party.example.com/data');
      expect(request.request.withCredentials).toBeFalse();

      request.flush({});
    });
  });
});
