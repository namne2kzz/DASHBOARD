import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import {
  RoleDto,
  CreateRolePayload,
  UpdateRolePayload,
} from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  readonly roles   = signal<RoleDto[]>([]);
  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (repoId) this.load(repoId);
    });
  }

  /** Loads all roles (default + custom) for the repository. @param repoId Repository ID. */
  load(repoId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<RoleDto[]>(this.url(repoId)).subscribe({
      next:  roles => { this.roles.set(roles.map(r => this.normalize(r))); this.loading.set(false); },
      error: ()    => { this.error.set('Failed to load roles'); this.loading.set(false); },
    });
  }

  /** Guarantees permissions is always an array, even when the API omits it. */
  private normalize(role: RoleDto): RoleDto {
    return { ...role, permissions: role.permissions ?? [] };
  }

  /**
   * Creates a new custom role.
   * @param repoId Repository ID.
   * @param payload Role name, description, and permissions.
   * @returns Observable of the created RoleDto.
   */
  create(repoId: string, payload: CreateRolePayload): Observable<RoleDto> {
    return this.http.post<RoleDto>(this.url(repoId), payload).pipe(
      tap(saved => this.roles.update(list => [...list, this.normalize(saved)])),
    );
  }

  /**
   * Updates an existing custom role. The API responds 204 No Content, so the
   * local list is patched optimistically from the payload.
   * @param repoId Repository ID.
   * @param roleId Role ID to update.
   * @param payload Updated name, description, and permissions.
   * @returns Observable completing on success.
   */
  update(repoId: string, roleId: string, payload: UpdateRolePayload): Observable<void> {
    return this.http.put<void>(`${this.url(repoId)}/${roleId}`, payload).pipe(
      tap(() => this.roles.update(list => list.map(r =>
        r.id === roleId
          ? { ...r, name: payload.name, description: payload.description, permissions: payload.permissions }
          : r))),
    );
  }

  /**
   * Deletes a custom role by ID.
   * @param repoId Repository ID.
   * @param roleId Role ID to delete.
   * @returns Observable completing on success.
   */
  delete(repoId: string, roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.url(repoId)}/${roleId}`).pipe(
      tap(() => this.roles.update(list => list.filter(r => r.id !== roleId))),
    );
  }

  /**
   * Clones a role (default or custom) under a new name.
   * @param repoId Repository ID.
   * @param roleId Source role ID.
   * @param newName Name for the cloned role.
   * @returns Observable of the cloned RoleDto.
   */
  clone(repoId: string, roleId: string, newName: string): Observable<RoleDto> {
    return this.http.post<RoleDto>(`${this.url(repoId)}/${roleId}/clone`, { newName }).pipe(
      tap(cloned => this.roles.update(list => [...list, this.normalize(cloned)])),
    );
  }

  private url(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/roles`;
  }
}
