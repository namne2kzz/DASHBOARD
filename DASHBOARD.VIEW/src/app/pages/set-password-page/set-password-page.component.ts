import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';

type SetPasswordState = 'missing-token' | 'ready' | 'submitting' | 'error';

/** One password rule, shown live so the user is never guessing what is still missing. */
interface PasswordRule {
  readonly label: string;
  readonly met: boolean;
}

/**
 * Public, unauthenticated page the first admin of a new organisation lands on from their welcome
 * email. Redeeming the link both sets the password and starts the session, so there is no second
 * sign-in step.
 */
@Component({
  selector: 'app-set-password-page',
  standalone: true,
  imports: [FormsModule, RouterLink, BrandLogoComponent],
  templateUrl: './set-password-page.component.html',
  styleUrls: ['./set-password-page.component.css'],
})
export class SetPasswordPageComponent {
  private readonly auth    = inject(AuthService);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly router  = inject(Router);

  readonly state        = signal<SetPasswordState>('ready');
  readonly errorMessage = signal<string | null>(null);

  readonly password     = signal('');
  readonly confirm      = signal('');
  readonly showPassword = signal(false);

  /**
   * Set once the user leaves the password field, which is when an unmet rule becomes a mistake
   * rather than a step not yet reached. Until then the rules read as a checklist, not as errors.
   */
  readonly passwordTouched = signal(false);

  /**
   * Mirrors SetPasswordCommandValidator on the server. Duplicated deliberately: the server stays
   * the authority, while showing the rules here turns a round-trip rejection into live feedback.
   */
  readonly rules = computed<PasswordRule[]>(() => {
    const value = this.password();
    return [
      { label: 'At least 8 characters',   met: value.length >= 8 },
      { label: 'An uppercase letter',     met: /[A-Z]/.test(value) },
      { label: 'A lowercase letter',      met: /[a-z]/.test(value) },
      { label: 'A digit',                 met: /\d/.test(value) },
      { label: 'A special character',     met: /[!@#$%^&*()_+\-=[\]{}|;':",./<>?]/.test(value) },
    ];
  });

  readonly allRulesMet = computed(() => this.rules().every(r => r.met));

  /** How many rules are satisfied, shown as a running count beside the checklist. */
  readonly metCount = computed(() => this.rules().filter(r => r.met).length);

  /**
   * True when an unmet rule should read as a failure. An empty field the user has merely tabbed
   * through is not a mistake yet, so the rules stay neutral until something has been typed.
   */
  readonly showRuleErrors = computed(() => this.passwordTouched() && this.password().length > 0);

  /** True once a confirmation has been typed and it differs from the password. */
  readonly mismatch = computed(() => this.confirm().length > 0 && this.confirm() !== this.password());

  readonly canSubmit = computed(() =>
    this.allRulesMet() && this.confirm() === this.password() && this.state() !== 'submitting');

  private readonly rawToken: string;

  constructor() {
    // The token travels in the URL fragment, which browsers never send to the server and proxies
    // never log — the same approach as the invite-accept link.
    this.rawToken = window.location.hash.replace(/^#/, '');
    if (!this.rawToken) this.state.set('missing-token');
  }

  /** Toggles plain-text display of both password fields. */
  togglePassword(): void {
    this.showPassword.update(shown => !shown);
  }

  /** Redeems the token with the chosen password and enters the app on success. */
  submit(): void {
    if (!this.canSubmit()) return;

    this.state.set('submitting');
    this.errorMessage.set(null);

    this.auth.setPassword(this.rawToken, this.password()).subscribe({
      next: () => {
        this.repoCtx.load();
        void this.router.navigateByUrl('/');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(err.error?.error ?? 'Could not set your password. Please try again.');
        // Back to 'ready' rather than a terminal error state: a weak password or a slow network is
        // worth retrying, and only an invalid token is genuinely final — which the message says.
        this.state.set('ready');
      },
    });
  }
}
