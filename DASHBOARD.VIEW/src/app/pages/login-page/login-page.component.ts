import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login-page.component.html',
  styleUrls: ['./login-page.component.css'],
})
export class LoginPageComponent {
  private readonly auth    = inject(AuthService);
  private readonly route   = inject(ActivatedRoute);
  private readonly router  = inject(Router);
  private readonly repoCtx = inject(RepositoryContextService);

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

  /** Toggle password field visibility between plain text and masked. */
  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  /** Submit email/password credentials and navigate on success. */
  login(): void {
    this.error.set(null);
    this.loading.set(true);

    this.auth.login({ email: this.email(), password: this.password() }).subscribe({
      next: () => {
        this.loading.set(false);
        this.repoCtx.load();
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const target = returnUrl && !returnUrl.startsWith('/login') ? returnUrl : '/';
        void this.router.navigateByUrl(target);
      },
      error: () => {
        this.error.set('Invalid email or password.');
        this.loading.set(false);
      },
    });
  }
}
