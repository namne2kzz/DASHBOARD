import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { InvitationApiDto, InvitationListItemDto, InvitationStatus } from '../models/invitation.model';
import { LoginResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly http    = inject(HttpClient);
  private readonly repoCtx = inject(RepositoryContextService);

  /** Every invitation ever sent for the currently selected repository, newest first. */
  readonly invitations = signal<InvitationListItemDto[]>([]);
  readonly loading      = signal(false);

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      if (repoId) this.load(repoId);
    });
  }

  /** Loads the invitation list for the repository into the signal. @param repoId Repository ID. */
  load(repoId: string): void {
    this.loading.set(true);
    this.http.get<InvitationListItemDto[]>(this.url(repoId)).subscribe({
      next:  invitations => { this.invitations.set(invitations); this.loading.set(false); },
      error: ()          => { this.loading.set(false); },
    });
  }

  /**
   * Sends a repository invitation to an external email. Revokes any prior pending invite for the
   * same email + repository on the backend. The chosen discipline + role are granted to the
   * invitee once they accept. Reloads the list on success, since a prior pending invite for the
   * same email may have just been revoked server-side.
   * @param repoId Repository ID.
   * @param email Email address to invite.
   * @param defaultRole Team role (discipline) to assign on acceptance.
   * @param roleId Role (default or custom) granting permissions, applied on acceptance.
   * @returns Observable of the created InvitationApiDto.
   */
  sendInvite(repoId: string, email: string, defaultRole: string, roleId: string): Observable<InvitationApiDto> {
    return this.http.post<InvitationApiDto>(this.url(repoId), { email, defaultRole, roleId }).pipe(
      tap(() => this.load(repoId)),
    );
  }

  /**
   * Revokes a still-pending invitation, e.g. one sent by mistake or no longer needed.
   * @param repoId Repository ID.
   * @param invitationId Invitation to revoke.
   * @returns Observable completing on success.
   */
  revoke(repoId: string, invitationId: string): Observable<void> {
    return this.http.post<void>(`${this.url(repoId)}/${invitationId}/revoke`, {}).pipe(
      tap(() => this.invitations.update(list =>
        list.map(i => i.id === invitationId ? { ...i, status: InvitationStatus.Revoked } : i))),
    );
  }

  /**
   * Accepts a repository invitation using the one-time token and a verified Google id_token.
   * On success the backend returns a signed-in session (invited users have no password).
   * @param rawToken The one-time token from the invite link's URL fragment.
   * @param googleIdToken The Google id_token obtained after the user signed in with Google.
   * @returns Observable of the LoginResponse session payload.
   */
  accept(rawToken: string, googleIdToken: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}/invitations/accept`, {
      rawToken,
      googleIdToken,
    });
  }

  private url(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/invitations`;
  }
}
