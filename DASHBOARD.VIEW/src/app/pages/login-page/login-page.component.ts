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

  readonly email        = signal('');
  readonly password     = signal('');
  readonly error        = signal<string | null>(null);
  readonly loading      = signal(false);
  readonly showPassword = signal(false);

  constructor() {
    if (this.auth.isAuthenticated()) {
      void this.router.navigate(['/']);
    }
  }

  /**
   * Renders the real Google Sign-In button invisibly on top of the custom-styled Google button
   * so the original design is preserved while clicks still trigger real Google auth.
   */
  ngAfterViewInit(): void {
    if (!this.googleButtonRef) return;
    this.googleIdentity.renderSignInButton(this.googleButtonRef.nativeElement, { type: 'standard', size: 'large', width: 130 })
      .then(idToken => this.googleSignIn(idToken))
      .catch((err: unknown) => this.error.set(err instanceof Error ? err.message : 'Google Sign-In is unavailable right now.'));
  }

  /** Toggle password field visibility between plain text and masked. */
  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  /** Submit email/password credentials and navigate on success. */
  login(): void {
    this.error.set(null);
    this.loading.set(true);

    this.auth.login({ email: this.email(), password: this.password() }).subscribe({
      next: () => this.enterApp(),
      error: () => {
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
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const target = returnUrl && !returnUrl.startsWith('/login') ? returnUrl : '/';
    void this.router.navigateByUrl(target);
  }
}
