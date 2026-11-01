import { AfterViewInit, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';

/** Which half of the two-step sign-in is showing. */
type LoginStep = 'organization' | 'credentials';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule, BrandLogoComponent],
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

  /**
   * Which half of the two-step sign-in is on screen. Step 1 names the organization and puts it in
   * the URL as /{orgAlias}/login, so every later step — and the link in a provisioning email —
   * already identifies the tenant.
   */
  readonly step = signal<LoginStep>(
    this.route.snapshot.paramMap.get('orgAlias') ? 'credentials' : 'organization');

  readonly orgAlias = signal(
    this.route.snapshot.paramMap.get('orgAlias')
    ?? this.route.snapshot.queryParamMap.get('org')
    ?? '');

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
      const external = this.externalReturnUrl();
      if (external) { this.redirectExternally(external); return; }
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
   * Step 1: confirms the organization exists, then moves to /{orgAlias}/login so the tenant is
   * named in the URL for the credentials step.
   *
   * Validating here rather than on submit means a mistyped alias is reported on its own, instead
   * of surfacing later as an indistinguishable "invalid credentials" after the user has also typed
   * their email and password.
   * @param orgAlias Organization alias as currently shown in its field.
   */
  continueWithOrganization(orgAlias: string): void {
    // As in login(): the DOM wins over the signal, so a browser autofill is not submitted empty.
    const org = (orgAlias || this.orgAlias()).trim().toLowerCase();
    this.orgAlias.set(org);

    if (!org) {
      this.error.set('Enter your organization name.');
      return;
    }

    // Checked here as well as on the server, so a typo costs nothing from the lookup's deliberately
    // small rate-limit budget — which a user correcting their own alias needs to keep.
    if (!/^[a-z0-9][a-z0-9-]*$/.test(org)) {
      this.error.set('Use only lowercase letters, digits and hyphens.');
      return;
    }

    this.error.set(null);
    this.loading.set(true);

    this.auth.lookupOrganization(org).subscribe({
      next: () => {
        this.loading.set(false);
        // Carry the query string over, so an interrupted deep link still resumes after sign-in.
        void this.router.navigate([org, 'login'], {
          queryParams: this.route.snapshot.queryParams,
        });
        this.step.set('credentials');
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.lookupErrorMessage(err.status));
      },
    });
  }

  /**
   * Turns a lookup failure into something the user can act on.
   *
   * A rejected shape and an unknown organization are different problems: telling someone who typed
   * an accented character that no such organization exists sends them hunting for the right name
   * when the characters are what needs fixing.
   * @param status The HTTP status the lookup failed with.
   * @returns The message to show beneath the field.
   */
  private lookupErrorMessage(status: number): string {
    switch (status) {
      case 422:
        return 'Use only lowercase letters, digits and hyphens.';
      case 429:
        return 'Too many attempts. Please wait a few minutes and try again.';
      case 0:
        return 'Could not reach the server. Check your connection and try again.';
      default:
        return 'No organization found with that name.';
    }
  }

  /** Returns to step 1 so a different organization can be entered, clearing what was typed. */
  changeOrganization(): void {
    this.error.set(null);
    this.email.set('');
    this.password.set('');
    this.step.set('organization');

    void this.router.navigate(['/login'], { queryParams: this.route.snapshot.queryParams });
  }

  /**
   * Submits credentials and navigates on success.
   *
   * The values are taken from the DOM rather than from the signals: a browser autofill writes
   * straight to `input.value` without raising the `input` event `ngModelChange` listens for, so a
   * field the user can plainly see filled in would otherwise be submitted empty.
   * @param email Email as currently shown in its field.
   * @param password Password as currently shown in its field.
   */
  login(email: string, password: string): void {
    // The DOM wins when it holds something, since that is what the user sees — including a value
    // the browser autofilled without raising an event. An empty field falls back to the signal,
    // which still carries a value ngModel has set but not yet flushed to the element.
    const mail = email    || this.email();
    const pass = password || this.password();

    // The alias is settled by step 1 and shown read-only, so it comes from the signal alone.
    const org = this.orgAlias();

    // Keep the signals in step, so error re-renders and later reads see what was sent.
    this.email.set(mail);
    this.password.set(pass);

    this.error.set(null);
    this.loading.set(true);

    this.auth.login({ orgAlias: org, email: mail, password: pass }).subscribe({
      next: () => this.enterApp(),
      error: () => {
        // The organization was confirmed in step 1, so only the credentials can be at fault here.
        this.error.set('Invalid email or password.');
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
    // Sign-in lives at both /login and /{orgAlias}/login, so matching the path segment catches
    // either — returning to a sign-in page we have just completed would only loop.
    const isSignInUrl = (url: string) => /(^|\/)login(\/|\?|#|$)/.test(url);
    const target = returnUrl && !isSignInUrl(returnUrl) ? returnUrl : '/';
    void this.router.navigateByUrl(target);
  }

  /**
   * If the query has an external (http/https) returnUrl — an add-on app like Nexus HUB —
   * redirects there with the current tokens appended so it can bootstrap its own session.
   * HUB's AppComponent stores the tokens and strips them from the URL immediately.
   *
   * The add-on lives on its own origin, so this handoff cannot ride on the cookie: that is what
   * the tokens in the URL are for. They are only in memory for the page-load that signed in, so
   * an already-signed-in visitor arriving here after a reload has none — see
   * {@link redirectExternally}, which mints a fresh pair first.
   *
   * @returns true if a redirect was issued (caller should stop), false otherwise.
   */
  private tryExternalReturn(): boolean {
    const returnUrl = this.externalReturnUrl();
    if (!returnUrl) return false;

    const accessToken = this.auth.getToken();
    if (!accessToken) return false;

    this.redirectTo(returnUrl, accessToken, this.auth.getRefreshToken() ?? '');
    return true;
  }

  /**
   * The external returnUrl from the query string, if there is one.
   * @returns The http/https URL to hand off to, or null.
   */
  private externalReturnUrl(): string | null {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl && /^https?:\/\//.test(returnUrl) ? returnUrl : null;
  }

  /**
   * Hands an already-signed-in visitor off to an add-on app, minting tokens if needed.
   *
   * The common case is someone using NFlow who clicks through to NHub: they have a valid session
   * cookie but no token in memory, because this page was loaded fresh rather than reached by
   * signing in. Exchanging the refresh cookie for a new pair is what makes the handoff possible
   * at all — without it the SSO attempt silently gave up and bounced them to the dashboard home,
   * so the add-on could never start a session.
   *
   * @param returnUrl The add-on URL to return to.
   */
  private redirectExternally(returnUrl: string): void {
    const token = this.auth.getToken();
    if (token) {
      this.redirectTo(returnUrl, token, this.auth.getRefreshToken() ?? '');
      return;
    }

    this.loading.set(true);
    this.auth.refresh().subscribe({
      next: session => this.redirectTo(returnUrl, session.accessToken, session.refreshToken),
      // The cookie is gone or rejected, so there is no session to pass on. Falling through to the
      // sign-in form lets them create one; the returnUrl survives in the query string, so the
      // handoff happens on the far side of that.
      error: () => this.loading.set(false),
    });
  }

  /**
   * Sends the browser to the add-on with the tokens appended.
   * @param returnUrl The add-on URL. @param accessToken Access token. @param refreshToken Refresh token.
   */
  private redirectTo(returnUrl: string, accessToken: string, refreshToken: string): void {
    const sep = returnUrl.includes('?') ? '&' : '?';
    window.location.href =
      `${returnUrl}${sep}t=${encodeURIComponent(accessToken)}&r=${encodeURIComponent(refreshToken)}`;
  }
}
