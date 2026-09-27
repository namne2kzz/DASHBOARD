import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { AuthService } from './auth.service';
import { PreferencesService } from '../core/services/preferences.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { LoginResponse } from '../models/auth.model';
import { UserProfile } from '../models/user.model';
import { environment } from '../../environments/environment';

/**
 * Unit tests for AuthService — the gate every other flow depends on.
 *
 * Two things shape this file. First, `isAuthenticated` and `currentUser` are initialised from
 * localStorage in the field initialisers, which run when the service is constructed; a test that
 * wants a pre-existing session therefore has to seed storage *before* injecting. Second,
 * `applySession` kicks off `PreferencesService.syncFromServer()`, so the real service is replaced
 * with a stub — otherwise every login test would leave an unexpected outstanding request and fail
 * `httpMock.verify()` for a reason that has nothing to do with authentication.
 *
 * The StorageService itself is left real. It is a thin wrapper over localStorage, and stubbing it
 * would hide the prefixing that decides whether a session actually survives a reload.
 */
describe('AuthService', () => {
  let httpMock: HttpTestingController;
  let syncFromServer: jasmine.Spy;

  const loginUrl   = `${environment.apiBaseUrl}/auth/login`;
  const googleUrl  = `${environment.apiBaseUrl}/auth/google-login`;
  const refreshUrl = `${environment.apiBaseUrl}/auth/refresh`;

  /** The prefix StorageService puts in front of every key. */
  const prefixed = (key: string) => `dashboard.${key}`;

  const session: LoginResponse = {
    accessToken:           'access.token.v1',
    jwtId:                 'jwt-1',
    accessTokenExpiresAt:  '2026-10-01T00:00:00Z',
    refreshToken:          'refresh.token.v1',
    refreshTokenExpiresAt: '2026-10-08T00:00:00Z',
    userId:                'user-1',
    name:                  'Nam',
    email:                 'nam@acme.local',
    isGlobalAdmin:         false,
    orgId:                 'org-1',
    orgAlias:              'acme',
    avatarClass:           'bg-emerald-600',
  };

  /**
   * Builds the service. Called explicitly rather than in `beforeEach` so each test can seed
   * localStorage first and thereby control what the constructor sees.
   */
  function createService(preferenceAvatarUrl: string | null = null): AuthService {
    syncFromServer = jasmine.createSpy('syncFromServer').and.returnValue(of(preferenceAvatarUrl));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        AuthService,
        { provide: PreferencesService, useValue: { syncFromServer } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    httpMock?.verify();
    localStorage.clear();
  });

  // ── Initial state ───────────────────────────────────────────────────────

  describe('on construction with empty storage', () => {
    it('reports nobody as authenticated', () => {
      const auth = createService();

      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.currentUser()).toBeNull();
    });
  });

  describe('on construction with a stored session', () => {
    it('restores the session so a page reload stays signed in', () => {
      const stored: UserProfile = {
        userId: 'user-9', email: 'back@acme.local', name: 'Returning',
        avatarClass: 'bg-sky-600', isGlobalAdmin: true, orgId: 'org-1', orgAlias: 'acme',
      };
      localStorage.setItem(prefixed(StorageKeys.accessToken), 'restored.token');
      localStorage.setItem(prefixed(StorageKeys.userProfile), JSON.stringify(stored));

      const auth = createService();

      expect(auth.isAuthenticated()).toBeTrue();
      expect(auth.currentUser()).toEqual(stored);
      expect(auth.getToken()).toBe('restored.token');
    });

    it('survives a corrupt profile without throwing', () => {
      // StorageService.get swallows a JSON parse failure and returns null. The token is still
      // there, so the user counts as authenticated but with no profile — the app must not crash
      // on construction.
      localStorage.setItem(prefixed(StorageKeys.accessToken), 'restored.token');
      localStorage.setItem(prefixed(StorageKeys.userProfile), '{not json');

      const auth = createService();

      expect(auth.isAuthenticated()).toBeTrue();
      expect(auth.currentUser()).toBeNull();
    });
  });

  // ── login ───────────────────────────────────────────────────────────────

  describe('login', () => {
    it('posts the credentials to the login endpoint', () => {
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();

      const request = httpMock.expectOne(loginUrl);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!',
      });

      request.flush(session);
    });

    it('stores both tokens and the profile', () => {
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(localStorage.getItem(prefixed(StorageKeys.accessToken))).toBe('access.token.v1');
      expect(localStorage.getItem(prefixed(StorageKeys.refreshToken))).toBe('refresh.token.v1');
      expect(auth.getToken()).toBe('access.token.v1');
      expect(auth.getRefreshToken()).toBe('refresh.token.v1');
    });

    it('raises the authentication signals', () => {
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(auth.isAuthenticated()).toBeTrue();
      expect(auth.currentUser()).toEqual({
        userId: 'user-1', email: 'nam@acme.local', name: 'Nam',
        avatarClass: 'bg-emerald-600', isGlobalAdmin: false, orgId: 'org-1', orgAlias: 'acme',
      });
    });

    it('keeps the organization on the profile', () => {
      // Every route in the app is scoped by orgAlias, so losing it here strands the user at the
      // root redirect with nowhere to go.
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(auth.currentUser()?.orgAlias).toBe('acme');
      expect(auth.currentUser()?.orgId).toBe('org-1');
    });

    it('does not store anything when the credentials are rejected', () => {
      const auth = createService();
      let failed = false;

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'wrong' })
          .subscribe({ error: () => (failed = true) });

      httpMock.expectOne(loginUrl).flush(
        { error: 'Invalid credentials.' },
        { status: 401, statusText: 'Unauthorized' },
      );

      expect(failed).toBeTrue();
      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.getToken()).toBeNull();
      expect(localStorage.getItem(prefixed(StorageKeys.refreshToken))).toBeNull();
    });

    it('pulls display preferences once the session is established', () => {
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(syncFromServer).toHaveBeenCalledTimes(1);
    });

    it('patches the avatar URL the preferences call returns', () => {
      const auth = createService('https://cdn.local/avatars/user-1.png');

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(auth.currentUser()?.avatarUrl).toBe('https://cdn.local/avatars/user-1.png');
    });

    it('leaves the profile alone when there is no stored avatar', () => {
      const auth = createService(null);

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      expect(auth.currentUser()?.avatarUrl).toBeUndefined();
      expect(auth.currentUser()?.avatarClass).toBe('bg-emerald-600');
    });
  });

  describe('login for a response with no avatarClass', () => {
    it('falls back to the default chip colour', () => {
      // avatarClass drives a Tailwind class, so an undefined value would render an unstyled chip.
      const auth = createService();

      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush({ ...session, avatarClass: undefined });

      expect(auth.currentUser()?.avatarClass).toBe('bg-sky-600');
    });
  });

  // ── googleLogin ─────────────────────────────────────────────────────────

  describe('googleLogin', () => {
    it('posts the Google id token and applies the returned session', () => {
      const auth = createService();

      auth.googleLogin('google.id.token').subscribe();

      const request = httpMock.expectOne(googleUrl);
      expect(request.request.body).toEqual({ googleIdToken: 'google.id.token' });

      request.flush(session);

      expect(auth.isAuthenticated()).toBeTrue();
      expect(auth.getToken()).toBe('access.token.v1');
    });

    it('leaves the user signed out when no account is linked', () => {
      // The endpoint never creates an account, so an unknown Google identity is a plain failure.
      const auth = createService();
      let failed = false;

      auth.googleLogin('google.id.token').subscribe({ error: () => (failed = true) });

      httpMock.expectOne(googleUrl).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(failed).toBeTrue();
      expect(auth.isAuthenticated()).toBeFalse();
    });
  });

  // ── refresh ─────────────────────────────────────────────────────────────

  describe('refresh', () => {
    it('sends the stored refresh token', () => {
      localStorage.setItem(prefixed(StorageKeys.refreshToken), 'stored.refresh');
      const auth = createService();

      auth.refresh().subscribe();

      const request = httpMock.expectOne(refreshUrl);
      expect(request.request.body).toEqual({ refreshToken: 'stored.refresh' });

      request.flush(session);
    });

    it('sends an empty string when there is no stored token rather than omitting the field', () => {
      // The request body is typed, and the backend validator expects the property to be present.
      const auth = createService();

      auth.refresh().subscribe();

      const request = httpMock.expectOne(refreshUrl);
      expect(request.request.body).toEqual({ refreshToken: '' });

      request.flush(session);
    });

    it('replaces both tokens with the rotated pair', () => {
      localStorage.setItem(prefixed(StorageKeys.refreshToken), 'stored.refresh');
      const auth = createService();

      auth.refresh().subscribe();
      httpMock.expectOne(refreshUrl).flush({
        ...session, accessToken: 'access.token.v2', refreshToken: 'refresh.token.v2',
      });

      expect(auth.getToken()).toBe('access.token.v2');
      // A refresh that kept the old refresh token would work once and then fail forever, because
      // the backend rotates it on every use.
      expect(auth.getRefreshToken()).toBe('refresh.token.v2');
    });
  });

  // ── logout ──────────────────────────────────────────────────────────────

  describe('logout', () => {
    it('clears every trace of the session', () => {
      const auth = createService();
      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);

      auth.logout();

      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.currentUser()).toBeNull();
      expect(auth.getToken()).toBeNull();
      expect(auth.getRefreshToken()).toBeNull();
      expect(localStorage.getItem(prefixed(StorageKeys.userProfile))).toBeNull();
    });

    it('is safe to call when nobody is signed in', () => {
      // The error interceptor calls logout() on any 401, including one that arrives while the user
      // is already signed out.
      const auth = createService();

      expect(() => auth.logout()).not.toThrow();
      expect(auth.isAuthenticated()).toBeFalse();
    });

    it('leaves unrelated app storage in place', () => {
      // Logout must not wipe UI preferences such as the chosen theme.
      localStorage.setItem(prefixed(StorageKeys.theme), 'dark');
      const auth = createService();

      auth.logout();

      expect(localStorage.getItem(prefixed(StorageKeys.theme))).toBe('dark');
    });
  });

  // ── patchProfile ────────────────────────────────────────────────────────

  describe('patchProfile', () => {
    function signedIn(): AuthService {
      const auth = createService();
      auth.login({ orgAlias: 'acme', email: 'nam@acme.local', password: 'Str0ng!' }).subscribe();
      httpMock.expectOne(loginUrl).flush(session);
      return auth;
    }

    it('merges the patch into the current profile', () => {
      const auth = signedIn();

      auth.patchProfile({ name: 'Nam Phuong' });

      expect(auth.currentUser()?.name).toBe('Nam Phuong');
      expect(auth.currentUser()?.email).toBe('nam@acme.local');
    });

    it('persists the patch so it survives a reload', () => {
      const auth = signedIn();

      auth.patchProfile({ name: 'Nam Phuong', avatarClass: 'bg-rose-600' });

      const stored = JSON.parse(localStorage.getItem(prefixed(StorageKeys.userProfile))!);
      expect(stored.name).toBe('Nam Phuong');
      expect(stored.avatarClass).toBe('bg-rose-600');
    });

    it('does nothing when nobody is signed in', () => {
      const auth = createService();

      auth.patchProfile({ name: 'Ghost' });

      expect(auth.currentUser()).toBeNull();
      expect(localStorage.getItem(prefixed(StorageKeys.userProfile))).toBeNull();
    });
  });

  // ── applySession ────────────────────────────────────────────────────────

  describe('applySession', () => {
    it('is usable on its own, for flows that get a session outside the login form', () => {
      // Accepting an email invite returns a session without going through login; it reuses this
      // method rather than duplicating the storage logic.
      const auth = createService();

      auth.applySession(session);

      expect(auth.isAuthenticated()).toBeTrue();
      expect(auth.getToken()).toBe('access.token.v1');
      expect(syncFromServer).toHaveBeenCalledTimes(1);
    });

    it('replaces an existing session rather than merging with it', () => {
      const auth = createService();
      auth.applySession(session);

      auth.applySession({
        ...session, userId: 'user-2', name: 'Someone Else',
        email: 'other@globex.local', orgAlias: 'globex', orgId: 'org-2',
      });

      expect(auth.currentUser()?.userId).toBe('user-2');
      expect(auth.currentUser()?.orgAlias).toBe('globex');
    });
  });
});
