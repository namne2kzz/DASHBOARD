import { AfterViewInit, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login-page.component.html',
  styleUrls: ['./login-page.component.css'],
})
export class LoginPageComponent implements AfterViewInit {
  private readonly auth           = inject(AuthService);
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly repoCtx        = inject(RepositoryContextService);
  private readonly googleIdentity = inject(GoogleIdentityService);

  @ViewChild('googleButton') private googleButtonRef?: ElementRef<HTMLDivElement>;

  readonly orgAlias     = signal(this.route.snapshot.queryParamMap.get('org') ?? '');
  readonly email        = signal('');
  readonly password     = signal('');
  readonly error        = signal<string | null>(null);
  readonly loading      = signal(false);
  readonly showPassword = signal(false);

  constructor() {
    const qp = this.route.snapshot.queryParamMap;

    // Single logout: an add-on app (e.g. Nexus HUB) sends the user here with ?logout=1
    // to also clear the DASHBOARD session. Clear it, then bounce back to the add-on's
    // signed-out page if a returnUrl was supplied (no tokens — the user is logged out).
    if (qp.get('logout') === '1') {
      this.auth.logout();
      const ret = qp.get('returnUrl');
      if (ret && /^https?:\/\//.test(ret)) { window.location.href = ret; return; }
      return;
    }

    // Already signed in: honour an external returnUrl (SSO pass-through to an add-on app)
    // instead of bouncing to the dashboard home — otherwise HUB can never get its tokens.
    if (this.auth.isAuthenticated()) {
      if (this.tryExternalReturn()) return;
      void this.router.navigate(['/']);
    }
  }

  /**
   * Renders the real Google Sign-In button invisibly on top of the custom-styled Google button
   * so the original design is preserved while clicks still trigger real Google auth.
   */
  ngAfterViewInit(): void {
    if (!this.googleButtonRef) return;
    // Silently ignore init failures (Google SDK not configured in dev).
    // Only propagate errors when the user actively clicks and gets an idToken.
    this.googleIdentity.renderSignInButton(this.googleButtonRef.nativeElement, { type: 'standard', size: 'large', width: 130 })
      .then(idToken => this.googleSignIn(idToken))
      .catch(() => { /* Google Sign-In unavailable — hide button, user can still use email/password */ });
  }

  /** Toggle password field visibility between plain text and masked. */
  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  /**
   * Submits credentials and navigates on success.
   *
   * The values are taken from the DOM rather than from the signals: a browser autofill writes
   * straight to `input.value` without raising the `input` event `ngModelChange` listens for, so a
   * field the user can plainly see filled in would otherwise be submitted empty.
   * @param orgAlias Organization alias as currently shown in its field.
   * @param email Email as currently shown in its field.
   * @param password Password as currently shown in its field.
   */
  login(orgAlias: string, email: string, password: string): void {
    // The DOM wins when it holds something, since that is what the user sees — including a value
    // the browser autofilled without raising an event. An empty field falls back to the signal,
    // which still carries a value ngModel has set but not yet flushed to the element (the ?org=
    // prefill on first render).
    const org  = orgAlias || this.orgAlias();
    const mail = email    || this.email();
    const pass = password || this.password();

    // Keep the signals in step, so error re-renders and later reads see what was sent.
    this.orgAlias.set(org);
    this.email.set(mail);
    this.password.set(pass);

    this.error.set(null);
    this.loading.set(true);

    this.auth.login({ orgAlias: org, email: mail, password: pass }).subscribe({
      next: () => this.enterApp(),
      error: () => {
        this.error.set('Invalid organization, email or password.');
        this.loading.set(false);
      },
    });
  }

  /**
   * Signs in with a verified Google id_token. Fails when no account is linked to that Google
   * identity yet — Google login only works for accounts created via a prior invite accept.
   * @param googleIdToken The id_token obtained from Google Sign-In.
   */
  private googleSignIn(googleIdToken: string): void {
    this.error.set(null);
    this.loading.set(true);

    this.auth.googleLogin(googleIdToken).subscribe({
      next: () => this.enterApp(),
      error: (err: HttpErrorResponse) => {
        this.error.set(err.error?.error ?? 'Google sign-in failed. Please try again.');
        this.loading.set(false);
      },
    });
  }

  /** Loads repository context and navigates into the app after a successful sign-in. */
  private enterApp(): void {
    this.loading.set(false);
    this.repoCtx.load();

    if (this.tryExternalReturn()) return;

    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const target = returnUrl && !returnUrl.startsWith('/login') ? returnUrl : '/';
    void this.router.navigateByUrl(target);
  }

  /**
   * If the query has an external (http/https) returnUrl — an add-on app like Nexus HUB —
   * redirects there with the current tokens appended so it can bootstrap its own session.
   * HUB's AppComponent stores the tokens and strips them from the URL immediately.
   * @returns true if a redirect was issued (caller should stop), false otherwise.
   */
  private tryExternalReturn(): boolean {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (!returnUrl || !/^https?:\/\//.test(returnUrl)) return false;

    const accessToken  = this.auth.getToken() ?? '';
    const refreshToken = this.auth.getRefreshToken() ?? '';
    if (!accessToken) return false;

    const sep = returnUrl.includes('?') ? '&' : '?';
    window.location.href =
      `${returnUrl}${sep}t=${encodeURIComponent(accessToken)}&r=${encodeURIComponent(refreshToken)}`;
    return true;
  }
}
