import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginRequest, LoginResponse, RefreshTokenRequest } from '../models/auth.model';
import { UserProfile } from '../models/user.model';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { PreferencesService } from '../core/services/preferences.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http        = inject(HttpClient);
  private readonly storage     = inject(StorageService);
  private readonly preferences = inject(PreferencesService);
  private readonly apiUrl      = `${environment.apiBaseUrl}/auth`;

  readonly isAuthenticated = signal<boolean>(this.storage.has(StorageKeys.accessToken));
  readonly currentUser     = signal<UserProfile | null>(this.storage.get<UserProfile>(StorageKeys.userProfile));

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

  /** Exchanges refresh token for a new access token. @returns Observable of LoginResponse. */
  refresh(): Observable<LoginResponse> {
    const refreshToken = this.storage.getString(StorageKeys.refreshToken) ?? '';
    return this.http.post<LoginResponse>(`${this.apiUrl}/refresh`, { refreshToken } as RefreshTokenRequest).pipe(
      tap(res => this.applySession(res))
    );
  }

  /** Clears session from storage and resets signals. */
  logout(): void {
    this.storage.remove(StorageKeys.accessToken);
    this.storage.remove(StorageKeys.refreshToken);
    this.storage.remove(StorageKeys.userProfile);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
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

  /** @returns The stored JWT access token, or null. */
  getToken(): string | null {
    return this.storage.getString(StorageKeys.accessToken);
  }

  /** @returns The stored refresh token, or null. Used by SSO redirect to add-on apps (e.g. Nexus HUB). */
  getRefreshToken(): string | null {
    return this.storage.getString(StorageKeys.refreshToken);
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
    };
    this.storage.setString(StorageKeys.accessToken, res.accessToken);
    this.storage.setString(StorageKeys.refreshToken, res.refreshToken);
    this.storage.set(StorageKeys.userProfile, profile);
    this.currentUser.set(profile);
    this.isAuthenticated.set(true);
    // Pull persisted display preferences (and avatar URL) from the server in the background.
    // syncFromServer() returns the stored avatar URL so we can patch the profile without
    // circular DI (PreferencesService cannot inject AuthService).
    this.preferences.syncFromServer().subscribe(avatarUrl => {
      if (avatarUrl) this.patchProfile({ avatarUrl });
    });
  }
}
