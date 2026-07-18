import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { InvitationsService } from '../../services/invitations.service';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';

type AcceptState = 'missing-token' | 'ready' | 'processing' | 'error';

/** Public, unauthenticated page a repository invitee lands on from their invite email. */
@Component({
  selector: 'app-invite-accept-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './invite-accept-page.component.html',
  styleUrls: ['./invite-accept-page.component.css'],
})
export class InviteAcceptPageComponent implements OnInit, AfterViewInit {
  private readonly googleIdentity = inject(GoogleIdentityService);
  private readonly invitations    = inject(InvitationsService);
  private readonly auth           = inject(AuthService);
  private readonly repoCtx        = inject(RepositoryContextService);
  private readonly router         = inject(Router);

  @ViewChild('googleButton') private googleButtonRef?: ElementRef<HTMLDivElement>;

  readonly state        = signal<AcceptState>('ready');
  readonly errorMessage = signal<string | null>(null);

  private rawToken = '';

  /** Reads the one-time invite token from the URL fragment — never the query string, so it's excluded from server/proxy logs. */
  ngOnInit(): void {
    this.rawToken = window.location.hash.replace(/^#/, '');
    if (!this.rawToken) {
      this.state.set('missing-token');
    }
  }

  /** Renders the Google Sign-In button once the container is in the DOM (only reached when a token is present). */
  ngAfterViewInit(): void {
    if (this.state() !== 'ready' || !this.googleButtonRef) return;

    this.googleIdentity.renderSignInButton(this.googleButtonRef.nativeElement)
      .then(idToken => this.accept(idToken))
      .catch((err: unknown) => this.fail(err instanceof Error ? err.message : 'Google Sign-In failed to load.'));
  }

  /**
   * Accepts the invitation with the verified Google id_token, starts a signed-in session on
   * success, and enters the app.
   * @param googleIdToken The id_token obtained from Google Sign-In.
   */
  private accept(googleIdToken: string): void {
    this.state.set('processing');
    this.invitations.accept(this.rawToken, googleIdToken).subscribe({
      next: res => {
        this.auth.applySession(res);
        this.repoCtx.load();
        void this.router.navigateByUrl('/');
      },
      error: (err: HttpErrorResponse) => {
        this.fail(err.error?.error ?? 'Failed to accept the invitation. Please try again.');
      },
    });
  }

  /** Moves to the error state with a user-facing message. @param message Error text to display. */
  private fail(message: string): void {
    this.errorMessage.set(message);
    this.state.set('error');
  }
}
