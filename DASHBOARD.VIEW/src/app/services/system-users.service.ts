import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { SystemUserDto, CreateUserPayload } from '../models/system-user.model';
import { UserPickerItem } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class SystemUsersService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  readonly users   = signal<SystemUserDto[]>([]);
  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  /** Loads all system users. Called lazily when the admin page is first visited. */
  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<{ items: SystemUserDto[] }>(`${this.baseUrl}?pageSize=500`).subscribe({
      next:  res => { this.users.set(res.items ?? []); this.loading.set(false); },
      error: ()  => { this.error.set('Failed to load users'); this.loading.set(false); },
    });
  }

  /**
   * Creates a new user account.
   * @param payload Name, email, password, and admin flag.
   * @returns Observable of the created SystemUserDto.
   */
  create(payload: CreateUserPayload): Observable<SystemUserDto> {
    return this.http.post<SystemUserDto>(this.baseUrl, payload).pipe(
      tap(user => this.users.update(list => [...list, user])),
    );
  }

  /**
   * Deactivates a user account (soft-disable).
   * @param userId Target user ID.
   * @returns Observable completing on success.
   */
  deactivate(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${userId}/deactivate`, {}).pipe(
      tap(() => this.patchUser(userId, { isActive: false })),
    );
  }

  /**
   * Re-activates a previously deactivated user account.
   * @param userId Target user ID.
   * @returns Observable completing on success.
   */
  activate(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${userId}/activate`, {}).pipe(
      tap(() => this.patchUser(userId, { isActive: true })),
    );
  }

  /**
   * Searches all active system users by name or email — no repo exclusion applied.
   * Used when assigning a Scrum Master before the repository exists.
   * @param q Search term (min 2 chars).
   * @returns Observable of matching UserPickerItem list.
   */
  searchAllUsers(q: string): Observable<UserPickerItem[]> {
    const params = new HttpParams().set('q', q);
    return this.http.get<UserPickerItem[]>(`${environment.apiBaseUrl}/users/search`, { params });
  }

  /**
   * Promotes a user to global admin.
   * @param userId Target user ID.
   * @returns Observable completing on success.
   */
  promoteAdmin(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${userId}/promote-admin`, {}).pipe(
      tap(() => this.patchUser(userId, { isGlobalAdmin: true })),
    );
  }

  /**
   * Demotes a user from global admin.
   * @param userId Target user ID.
   * @returns Observable completing on success.
   */
  demoteAdmin(userId: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${userId}/demote-admin`, {}).pipe(
      tap(() => this.patchUser(userId, { isGlobalAdmin: false })),
    );
  }

  /**
   * Extracts two-letter initials from a full name.
   * @param name Full display name.
   * @returns Uppercase initials string.
   */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  private patchUser(userId: string, patch: Partial<SystemUserDto>): void {
    this.users.update(list => list.map(u => u.userId === userId ? { ...u, ...patch } : u));
  }
}
