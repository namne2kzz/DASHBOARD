import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginPageComponent } from './login-page.component';
import { AuthService } from '../../services/auth.service';
import { LoginResponse } from '../../models/auth.model';
import { RepositoryContextService } from '../../services/repository-context.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';

/**
 * Submission is exercised by writing to the real input elements and clicking the real button,
 * because the defect these tests guard against lives precisely in the gap between the DOM value
 * and the component's signals — a test that called `login(...)` directly could not observe it.
 */
describe('LoginPageComponent', () => {
  let fixture: ComponentFixture<LoginPageComponent>;
  let auth: jasmine.SpyObj<AuthService>;

  const session: LoginResponse = {
    accessToken: 'token',
    jwtId: 'jwt',
    accessTokenExpiresAt: '2026-10-02T00:00:00Z',
    refreshToken: 'refresh',
    refreshTokenExpiresAt: '2026-11-01T00:00:00Z',
    userId: 'u1',
    name: 'Admin User',
    email: 'admin@dashboard.local',
    isGlobalAdmin: true,
    orgId: 'org-1',
    orgAlias: 'default',
    avatarClass: 'bg-sky-600',
  };

  function setup(queryParams: Record<string, string> = {}): void {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'googleLogin', 'logout', 'isAuthenticated']);
    auth.isAuthenticated.and.returnValue(false);
    auth.login.and.returnValue(of(session));

    TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate', 'navigateByUrl']) },
        {
          provide: RepositoryContextService,
          useValue: jasmine.createSpyObj('RepositoryContextService', ['load']),
        },
        {
          provide: GoogleIdentityService,
          // Rendering the real Google button would need the external SDK; the login form under
          // test does not depend on it.
          useValue: { renderSignInButton: () => Promise.resolve('') },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    });

    fixture = TestBed.createComponent(LoginPageComponent);
    fixture.detectChanges();
  }

  function input(id: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement;
  }

  function submit(): void {
    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).find(b => (b as HTMLButtonElement).textContent?.includes('Sign in')) as HTMLButtonElement;

    button.click();
  }

  /** Types into a field the way a user does: the value changes and an input event follows. */
  function type(id: string, value: string): void {
    const el = input(id);
    el.value = value;
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  /**
   * Mimics a browser autofill: the value appears in the DOM with no `input` event, which is what
   * Chrome does when it fills a field from its saved-organization list.
   */
  function autofill(id: string, value: string): void {
    input(id).value = value;
  }

  it('submits what the user typed', () => {
    setup();

    type('orgAlias', 'default');
    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    submit();

    expect(auth.login).toHaveBeenCalledWith({
      orgAlias: 'default',
      email: 'admin@dashboard.local',
      password: 'Password123!',
    });
  });

  it('submits an autofilled organization instead of an empty string', () => {
    // The original defect: Chrome autofilled "default" into the field, the signal stayed at '',
    // and the backend rejected the login with an empty OrgAlias while the UI plainly showed it.
    setup();

    autofill('orgAlias', 'default');
    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    submit();

    expect(auth.login).toHaveBeenCalledWith(
      jasmine.objectContaining({ orgAlias: 'default' }),
    );
  });

  it('submits every field when all three are autofilled', () => {
    setup();

    autofill('orgAlias', 'default');
    autofill('email', 'admin@dashboard.local');
    autofill('password', 'Password123!');
    submit();

    expect(auth.login).toHaveBeenCalledWith({
      orgAlias: 'default',
      email: 'admin@dashboard.local',
      password: 'Password123!',
    });
  });

  it('prefills the organization from the ?org= query parameter', () => {
    // Invite links carry the tenant, so the field must arrive populated and submit that value.
    setup({ org: 'nexus' });

    // Asserted on the signal rather than the element: ngModel's write to the DOM does not settle
    // within TestBed's synchronous passes here. The submit assertion below is what actually
    // guarantees the prefilled tenant reaches the backend.
    expect(fixture.componentInstance.orgAlias()).toBe('nexus');

    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    submit();

    expect(auth.login).toHaveBeenCalledWith(
      jasmine.objectContaining({ orgAlias: 'nexus' }),
    );
  });

  it('sends a later edit rather than the prefilled value', () => {
    setup({ org: 'nexus' });

    type('orgAlias', 'default');
    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    submit();

    expect(auth.login).toHaveBeenCalledWith(
      jasmine.objectContaining({ orgAlias: 'default' }),
    );
  });

  it('shows an error and stops loading when the backend rejects the credentials', () => {
    setup();
    auth.login.and.returnValue(throwError(() => ({ status: 401 })));

    type('orgAlias', 'default');
    type('email', 'admin@dashboard.local');
    type('password', 'wrong');
    submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid organization, email or password.');
    expect(fixture.componentInstance.loading()).toBeFalse();
  });

  it('keeps the signals in step with what was submitted', () => {
    // Other parts of the template render from the signals; leaving them stale after an autofill
    // submit would show one thing while another was sent.
    setup();

    autofill('orgAlias', 'default');
    autofill('email', 'admin@dashboard.local');
    autofill('password', 'Password123!');
    submit();

    expect(fixture.componentInstance.orgAlias()).toBe('default');
    expect(fixture.componentInstance.email()).toBe('admin@dashboard.local');
  });
});
