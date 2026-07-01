import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { RoleService } from './role.service';
import { RepositoryContextService } from './repository-context.service';
import { MemberApiDto } from '../models/member.model';
import { UserPickerItem } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class MembersService {
  private readonly http = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly auth = inject(AuthService);
  private readonly roleService = inject(RoleService);

  readonly members = signal<MemberApiDto[]>([]);

  /** The logged-in user's ID from the JWT session. */
  readonly currentUserId = computed(() => this.auth.currentUser()?.userId ?? null);

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (repoId) this.load(repoId);
    });
  }

  /** Returns the display name for a user ID, or 'Unassigned' if not found. @param userId User ID. @returns Display name string. */
  displayName(userId: string | null | undefined): string {
    if (!userId) return 'Unassigned';
    return this.members().find(m => m.userId === userId)?.userName ?? userId;
  }

  /** Returns the Tailwind avatar CSS class for a user ID. @param userId User ID. @returns CSS class string. */
  avatarClass(userId: string | null | undefined): string {
    if (!userId) return 'bg-slate-600';
    return this.members().find(m => m.userId === userId)?.avatarClass ?? 'bg-slate-600';
  }

  /** Extracts two-letter initials from a full name. @param name Full display name. @returns Uppercase initials string. */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  /** Returns the member record for a user ID, or null if not found. @param userId User ID. @returns MemberApiDto or null. */
  getMember(userId: string | null | undefined): MemberApiDto | null {
    if (!userId) return null;
    return this.members().find(m => m.userId === userId) ?? null;
  }

  /** Resolves a member's display role label, preferring assigned role name over default team role. @param member MemberApiDto. @returns Human-readable role label. */
  roleName(member: MemberApiDto): string {
    return member.roleName ?? member.defaultRole;
  }

  /**
   * Updates a member's role assignment. The API replaces the default team role
   * and the custom role atomically (PUT, 204 No Content), so callers must pass
   * the full desired state; the local signal is patched optimistically.
   * @param repoId Repository ID.
   * @param memberId Member record ID.
   * @param defaultRole Desired team role (discipline).
   * @param roleId Desired role ID (default or custom) granting permissions. Required.
   * @returns Observable completing on success.
   */
  updateMemberRole(repoId: string, memberId: string, defaultRole: string, roleId: string): Observable<void> {
    return this.http
      .put<void>(`${environment.apiBaseUrl}/repositories/${repoId}/members/${memberId}/role`, { defaultRole, roleId })
      .pipe(tap(() => {
        const roleName = this.roleService.roles().find(r => r.id === roleId)?.name ?? null;
        this.members.update(list => list.map(m =>
          m.memberId === memberId ? { ...m, defaultRole, roleId, roleName } : m));
      }));
  }

  /**
   * Removes a member from the repository.
   * @param repoId Repository ID.
   * @param memberId Member record ID.
   * @returns Observable completing on success.
   */
  removeMember(repoId: string, memberId: string): Observable<void> {
    return this.http
      .delete<void>(`${environment.apiBaseUrl}/repositories/${repoId}/members/${memberId}`)
      .pipe(tap(() => this.members.update(list => list.filter(m => m.memberId !== memberId))));
  }

  /**
   * Searches active users by name/email, excluding users already in the repository.
   * @param repoId Repository ID — already-members are excluded from results.
   * @param q Search term (min 2 chars).
   * @returns Observable of matching UserPickerItem list.
   */
  searchUsers(repoId: string, q: string): Observable<UserPickerItem[]> {
    const params = new HttpParams().set('q', q).set('excludeRepoId', repoId);
    return this.http.get<UserPickerItem[]>(`${environment.apiBaseUrl}/users/search`, { params });
  }

  /**
   * Adds a user to the repository and appends the new member to the local signal.
   * @param repoId Repository ID.
   * @param userId User to add.
   * @param defaultRole Initial team role (discipline).
   * @param roleId Role ID (default or custom) granting permissions. Required.
   * @returns Observable of the created MemberApiDto.
   */
  addMember(repoId: string, userId: string, defaultRole: string, roleId: string): Observable<MemberApiDto> {
    return this.http
      .post<MemberApiDto>(`${environment.apiBaseUrl}/repositories/${repoId}/members`, { userId, defaultRole, roleId })
      .pipe(tap(m => this.members.update(list => [...list, m])));
  }

  /** Loads (or reloads) the member list for a repository into the signal. @param repoId Repository ID. */
  load(repoId: string): void {
    this.http
      .get<MemberApiDto[]>(`${environment.apiBaseUrl}/repositories/${repoId}/members`)
      .subscribe({ next: members => this.members.set(members) });
  }
}
