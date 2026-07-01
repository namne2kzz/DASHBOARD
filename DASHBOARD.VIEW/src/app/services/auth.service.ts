import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginRequest, LoginResponse, RefreshTokenRequest } from '../models/auth.model';
import { UserProfile } from '../models/user.model';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http    = inject(HttpClient);
  private readonly storage = inject(StorageService);
  private readonly apiUrl  = `${environment.apiBaseUrl}/auth`;

  readonly isAuthenticated = signal<boolean>(this.storage.has(StorageKeys.accessToken));
  readonly currentUser     = signal<UserProfile | null>(this.storage.get<UserProfile>(StorageKeys.userProfile));

  /** Authenticates with the backend and stores tokens. @returns Observable of LoginResponse. */
  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(res => this.persistSession(res))
    );
  }

  /** Exchanges refresh token for a new access token. @returns Observable of LoginResponse. */
  refresh(): Observable<LoginResponse> {
    const refreshToken = this.storage.getString(StorageKeys.refreshToken) ?? '';
    return this.http.post<LoginResponse>(`${this.apiUrl}/refresh`, { refreshToken } as RefreshTokenRequest).pipe(
      tap(res => this.persistSession(res))
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

  /** @returns The stored JWT access token, or null. */
  getToken(): string | null {
    return this.storage.getString(StorageKeys.accessToken);
  }

  private persistSession(res: LoginResponse): void {
    const profile: UserProfile = {
      userId: res.userId,
      email: res.email,
      name: res.name,
      avatarClass: '',
      isGlobalAdmin: res.isGlobalAdmin,
    };
    this.storage.setString(StorageKeys.accessToken, res.accessToken);
    this.storage.setString(StorageKeys.refreshToken, res.refreshToken);
    this.storage.set(StorageKeys.userProfile, profile);
    this.currentUser.set(profile);
    this.isAuthenticated.set(true);
  }
}
