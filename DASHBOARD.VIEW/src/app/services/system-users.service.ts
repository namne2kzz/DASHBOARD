import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { HttpCacheService } from './http-cache.service';
import { SystemUserDto, CreateUserPayload, UserHierarchy } from '../models/system-user.model';
import { UserPickerItem } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class SystemUsersService {
  private readonly http    = inject(HttpClient);
  private readonly cache   = inject(HttpCacheService);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  /** Search results are memoised briefly so backtracking a typeahead term costs no request. */
  private static readonly SearchTtlMs = 60_000;

  /** Hierarchy is memoised while the admin page is open; setManager() evicts it. */
  private static readonly HierarchyTtlMs = 120_000;

  /** Prefix for every cached hierarchy entry, so one manager change can evict them all. */
  private static readonly HierarchyPrefix = 'user-hierarchy:';

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
    // The caller debounces and drops consecutive duplicates, but that still refetches when the user
    // backtracks: "bo" → "bob" → "bo" issues the "bo" request twice. Memoising covers that.
    return this.cache.get(
      `user-search:${q}`,
      SystemUsersService.SearchTtlMs,
      () => {
        const params = new HttpParams().set('q', q);
        return this.http.get<UserPickerItem[]>(`${environment.apiBaseUrl}/users/search`, { params });
      });
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
   * Sets or clears a user's manager, then patches the local list.
   * @param userId Target user ID.
   * @param managerId New manager ID, or null to clear.
   * @returns Observable completing on success.
   */
  setManager(userId: string, managerId: string | null): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${userId}/manager`, { managerId }).pipe(
      tap(() => {
        const managerName = managerId
          ? this.users().find(u => u.userId === managerId)?.name ?? null
          : null;
        this.patchUser(userId, { managerId, managerName });

        // Evict every hierarchy, not just this user's: moving someone also reshapes the slice seen
        // from their old manager, their new manager, and their new peers.
        this.cache.invalidate(SystemUsersService.HierarchyPrefix);
      }),
    );
  }

  /**
   * Loads the organisation-chart slice centred on a user.
   * @param userId Focus user ID.
   * @returns Observable of the hierarchy.
   */
  getHierarchy(userId: string): Observable<UserHierarchy> {
    return this.cache.get(
      `${SystemUsersService.HierarchyPrefix}${userId}`,
      SystemUsersService.HierarchyTtlMs,
      () => this.http.get<UserHierarchy>(`${this.baseUrl}/${userId}/hierarchy`));
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
