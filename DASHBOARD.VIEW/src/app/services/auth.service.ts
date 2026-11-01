import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginRequest, LoginResponse, OrganizationLookup, RefreshTokenRequest } from '../models/auth.model';
import { UserProfile } from '../models/user.model';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { PreferencesService } from '../core/services/preferences.service';
import { HttpCacheService } from './http-cache.service';
import { NotificationService } from './notification.service';

/**
 * How far before the stated expiry a token is treated as spent, in milliseconds.
 *
 * Covers the request's own flight time plus any clock drift between this machine and the server:
 * without it a token expiring in 200ms would still be sent, rejected, and need the slower
 * 401-and-retry path after all.
 */
const ACCESS_TOKEN_EXPIRY_MARGIN_MS = 10_000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http          = inject(HttpClient);
  private readonly storage       = inject(StorageService);
  private readonly preferences   = inject(PreferencesService);
  private readonly httpCache     = inject(HttpCacheService);
  private readonly notiSvc       = inject(NotificationService);
  private readonly apiUrl        = `${environment.apiBaseUrl}/auth`;

  // Tokens are no longer persisted to storage — they live in HttpOnly cookies the browser sends
  // automatically. These in-memory copies exist only for flows that genuinely need the raw token
  // in JS: the HUB SSO handoff (cross-origin redirect) and the SSE ?access_token= fallback.
  // They are null after a page reload, which is fine: the cookie still authenticates API calls.
  private _accessToken:  string | null = null;
  private _refreshToken: string | null = null;

  /**
   * When the current access token stops being accepted, as epoch milliseconds.
   *
   * Known only for a session this tab signed in or refreshed; a reload leaves it null because the
   * token itself is in an HttpOnly cookie this code cannot read. Null therefore means "no idea",
   * not "expired" — see {@link isAccessTokenExpired}.
   */
  private _accessTokenExpiresAt: number | null = null;

  // Session presence is derived from a successfully-parsed persisted profile (not a secret), so a
  // reload restores the authenticated UI immediately; the HttpOnly cookie remains the actual
  // credential. A corrupt/absent profile means the UI treats the user as signed out.
  readonly currentUser     = signal<UserProfile | null>(this.storage.get<UserProfile>(StorageKeys.userProfile));
  readonly isAuthenticated = signal<boolean>(this.currentUser() !== null);

  constructor() {
    // A reload restores the session from the persisted profile without going through
    // applySession(), which is the only other place the notification stream is opened. Without
    // this the bell went quiet after every refresh — the stream stayed closed until the user
    // signed in again, even though the cookie was still perfectly good.
    if (this.isAuthenticated()) this.notiSvc.connect();
  }

  /**
   * Resolves the organization alias typed on the first step of sign-in. Errors with 404 when no
   * organization has that alias, so the user is told before they type any credentials.
   * @param alias The organization alias.
   * @returns Observable of the confirmed alias.
   */
  lookupOrganization(alias: string): Observable<OrganizationLookup> {
    return this.http.get<OrganizationLookup>(`${this.apiUrl}/orgs/${encodeURIComponent(alias)}`);
  }

  /** Authenticates with the backend and stores tokens. @returns Observable of LoginResponse. */
  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(res => this.applySession(res))
    );
  }

  /**
   * Authenticates an existing user via Google Sign-In and stores tokens. Never creates an account —
   * fails if no user is linked to that Google identity yet (they need an invite first).
   * @param googleIdToken The Google id_token obtained after the user signed in with Google.
   * @returns Observable of LoginResponse.
   */
  googleLogin(googleIdToken: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/google-login`, { googleIdToken }).pipe(
      tap(res => this.applySession(res))
    );
  }

  /**
   * Redeems a one-time password-setup link and signs the user in with the password they chose.
   * Used by the first admin of a newly provisioned organisation, whose account is created without
   * a password — holding the token proves control of the mailbox it was emailed to.
   * @param token The one-time token taken from the setup link's URL fragment.
   * @param newPassword The password the user has chosen.
   * @returns Observable of LoginResponse; the session starts on success.
   */
  setPassword(token: string, newPassword: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/set-password`, { token, newPassword }).pipe(
      tap(res => this.applySession(res))
    );
  }

  /**
   * Exchanges the refresh token for a new access token. The refresh token is sent as an HttpOnly
   * cookie the browser attaches automatically, so no body is needed; an empty object keeps the
   * endpoint's JSON content type.
   * @returns Observable of LoginResponse.
   */
  refresh(): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/refresh`, {} as RefreshTokenRequest).pipe(
      tap(res => this.applySession(res))
    );
  }

  /**
   * Ends the session. Fires a best-effort server logout so the HttpOnly cookies are expired and
   * the refresh token is revoked (a purely local clear would leave the cookies in place and the
   * next reload would re-authenticate), then resets all client-side state regardless of the
   * server result — the user is logging out whether or not the network call succeeds.
   */
  logout(): void {
    // Fire-and-forget: callers (menu, 401 interceptor) do not await this. withCredentials sends
    // the cookie so the server can revoke it; errors are swallowed since we clear locally anyway.
    this.http.post(`${this.apiUrl}/logout`, {}).subscribe({ error: () => {} });

    this.notiSvc.disconnect();
    this._accessToken = null;
    this._refreshToken = null;
    // Left behind, this would make the next sign-in look expired from its first request.
    this._accessTokenExpiresAt = null;
    this.storage.remove(StorageKeys.userProfile);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    // The HTTP cache is a singleton that outlives the session: without this, the next user to sign in
    // on this browser would be served the previous user's cached reads.
    this.httpCache.clear();
  }

  /**
   * Patches the cached user profile in memory and localStorage after a profile update.
   * Call after a successful PUT /users/{id}/profile so the new name and avatarClass
   * survive a page reload without requiring the user to log in again.
   * @param patch Partial profile fields to merge.
   */
  patchProfile(patch: Partial<UserProfile>): void {
    this.currentUser.update(u => {
      if (!u) return u;
      const updated = { ...u, ...patch };
      this.storage.set(StorageKeys.userProfile, updated);
      return updated;
    });
  }

  /**
   * @returns The in-memory JWT access token, or null after a reload. Authentication itself uses
   * the HttpOnly cookie; this is only for the SSE query-param fallback and the HUB SSO handoff,
   * both of which run within the same session where login just populated it.
   */
  getToken(): string | null {
    return this._accessToken;
  }

  /**
   * Whether the access token is known to be past its life, with a small margin.
   *
   * Lets a request refresh *before* it goes out instead of being sent, rejected, refreshed and
   * replayed — one round trip rather than three, and no 401 in the console for something that was
   * never wrong. The margin covers the time the request spends in flight, so a token that expires
   * while it is on the wire does not come back 401 anyway.
   *
   * False when the expiry is unknown (after a reload): the cookie may well still be valid, and
   * refreshing on a guess would spend the refresh token for nothing. Those sessions fall back to
   * the 401-and-retry path, which still works.
   *
   * @returns True only when the expiry is known and has passed.
   */
  get isAccessTokenExpired(): boolean {
    if (this._accessTokenExpiresAt === null) return false;
    return Date.now() >= this._accessTokenExpiresAt - ACCESS_TOKEN_EXPIRY_MARGIN_MS;
  }

  /** @returns The in-memory refresh token, or null after a reload. Used by the HUB SSO redirect only. */
  getRefreshToken(): string | null {
    return this._refreshToken;
  }

  /**
   * Stores a signed-in session and updates the auth signals. Public so flows that obtain a
   * session outside the regular login form (e.g. accepting an email invite) can reuse the same
   * storage logic instead of duplicating it.
   * @param res The session payload returned by the backend (login, refresh, or invite-accept).
   */
  applySession(res: LoginResponse): void {
    const profile: UserProfile = {
      userId: res.userId,
      email: res.email,
      name: res.name,
      avatarClass: res.avatarClass ?? 'bg-sky-600',
      isGlobalAdmin: res.isGlobalAdmin,
      orgId: res.orgId,
      orgAlias: res.orgAlias,
      // Falls back to the alias for sessions stored before the name was carried, so a profile
      // persisted by an older build still renders something sensible after an upgrade.
      orgName: res.orgName ?? res.orgAlias,
    };
    // Tokens stay in memory only (see field docs) — the server also set them as HttpOnly cookies,
    // which are what actually authenticate requests. The profile is not a secret and is persisted
    // so a reload restores the UI without a round-trip.
    this._accessToken  = res.accessToken;
    this._refreshToken = res.refreshToken;
    this._accessTokenExpiresAt = Date.parse(res.accessTokenExpiresAt) || null;
    this.storage.set(StorageKeys.userProfile, profile);
    this.currentUser.set(profile);
    this.isAuthenticated.set(true);
    // Pull persisted display preferences (and avatar URL) from the server in the background.
    // syncFromServer() returns the stored avatar URL so we can patch the profile without
    // circular DI (PreferencesService cannot inject AuthService).
    this.preferences.syncFromServer().subscribe(avatarUrl => {
      if (avatarUrl) this.patchProfile({ avatarUrl });
    });
    this.notiSvc.connect();
  }
}
