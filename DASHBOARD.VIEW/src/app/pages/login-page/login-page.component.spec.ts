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
 *
 * Sign-in is two steps: step 1 confirms the organization against the backend and routes to
 * /{orgAlias}/login, step 2 takes the credentials. Landing straight on step 2 is what a
 * provisioning email's link does, so both entry points are covered.
 */
describe('LoginPageComponent', () => {
  let fixture: ComponentFixture<LoginPageComponent>;
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

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

  /**
   * @param queryParams The `?org=`/`?returnUrl=` query string.
   * @param routeParams Route params; `orgAlias` present means the user landed on step 2 directly.
   */
  function setup(
    queryParams: Record<string, string> = {},
    routeParams: Record<string, string> = {},
  ): void {
    auth = jasmine.createSpyObj<AuthService>('AuthService',
      ['login', 'googleLogin', 'logout', 'isAuthenticated', 'lookupOrganization']);
    auth.isAuthenticated.and.returnValue(false);
    auth.login.and.returnValue(of(session));
    auth.lookupOrganization.and.returnValue(of({ alias: 'default' }));

    router = jasmine.createSpyObj<Router>('Router', ['navigate', 'navigateByUrl']);

    TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
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
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(queryParams),
              queryParams,
              paramMap: convertToParamMap(routeParams),
            },
          },
        },
      ],
    });

    fixture = TestBed.createComponent(LoginPageComponent);
    fixture.detectChanges();
  }

  /** Sets up on step 1 and walks through it, leaving the fixture showing the credentials step. */
  function setupOnCredentials(queryParams: Record<string, string> = {}): void {
    setup(queryParams);
    type('orgAliasStep', 'default');
    click('Continue');
    fixture.detectChanges();
  }

  function input(id: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement;
  }

  /** Clicks the first button whose label contains the given text. */
  function click(label: string): void {
    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).find(b => (b as HTMLButtonElement).textContent?.includes(label)) as HTMLButtonElement;

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

  // ── Step 1: organization ──────────────────────────────────────────────────

  it('starts on the organization step when no alias is in the route', () => {
    setup();

    expect(fixture.componentInstance.step()).toBe('organization');
    expect(input('orgAliasStep')).toBeTruthy();
  });

  it('confirms the organization with the backend before moving on', () => {
    setup();

    type('orgAliasStep', 'default');
    click('Continue');

    expect(auth.lookupOrganization).toHaveBeenCalledWith('default');
    expect(fixture.componentInstance.step()).toBe('credentials');
  });

  it('puts the confirmed organization in the URL', () => {
    // The tenant belongs in the address so a provisioning email, a bookmark and a refresh all
    // resume on the same organization.
    setup({ returnUrl: '/' });

    type('orgAliasStep', 'default');
    click('Continue');

    expect(router.navigate).toHaveBeenCalledWith(
      ['default', 'login'],
      jasmine.objectContaining({ queryParams: { returnUrl: '/' } }),
    );
  });

  it('normalises the organization before sending it', () => {
    setup();

    type('orgAliasStep', '  DEFAULT  ');
    click('Continue');

    expect(auth.lookupOrganization).toHaveBeenCalledWith('default');
  });

  it('submits an autofilled organization instead of an empty string', () => {
    // The original defect: Chrome autofilled "default" into the field, the signal stayed at '',
    // and the backend was called with an empty alias while the UI plainly showed it.
    setup();

    autofill('orgAliasStep', 'default');
    click('Continue');

    expect(auth.lookupOrganization).toHaveBeenCalledWith('default');
  });

  it('stays on step 1 and explains when the organization does not exist', () => {
    setup();
    auth.lookupOrganization.and.returnValue(throwError(() => ({ status: 404 })));

    type('orgAliasStep', 'nope');
    click('Continue');
    fixture.detectChanges();

    expect(fixture.componentInstance.step()).toBe('organization');
    expect(fixture.nativeElement.textContent).toContain('No organization found with that name.');
    expect(fixture.componentInstance.loading()).toBeFalse();
  });

  it('tells the user to wait when the lookup is rate limited', () => {
    // The endpoint is deliberately throttled against alias probing; a throttled visitor needs a
    // different message from someone who simply mistyped.
    setup();
    auth.lookupOrganization.and.returnValue(throwError(() => ({ status: 429 })));

    type('orgAliasStep', 'default');
    click('Continue');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Too many attempts.');
  });

  it('does not call the backend with an empty organization', () => {
    setup();

    click('Continue');

    expect(auth.lookupOrganization).not.toHaveBeenCalled();
  });

  it('rejects characters an alias cannot contain without spending a lookup', () => {
    // The lookup's rate-limit budget is small on purpose; a typo must not consume part of it.
    setup();

    type('orgAliasStep', 'ègg');
    click('Continue');
    fixture.detectChanges();

    expect(auth.lookupOrganization).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('lowercase letters, digits and hyphens');
  });

  it('distinguishes a malformed alias from an unknown one when the server rejects it', () => {
    // Should the client-side check ever drift from the server's rule, the message must still
    // point at the characters rather than sending the user hunting for the right name.
    setup();
    auth.lookupOrganization.and.returnValue(throwError(() => ({ status: 422 })));

    type('orgAliasStep', 'default');
    click('Continue');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('lowercase letters, digits and hyphens');
  });

  // ── Step 2: credentials ───────────────────────────────────────────────────

  it('opens straight on the credentials step when the route names an organization', () => {
    // This is the link a provisioning email sends: /{orgAlias}/login.
    setup({}, { orgAlias: 'acme' });

    expect(fixture.componentInstance.step()).toBe('credentials');
    expect(fixture.componentInstance.orgAlias()).toBe('acme');
    expect(auth.lookupOrganization).not.toHaveBeenCalled();
  });

  it('submits what the user typed, scoped to the confirmed organization', () => {
    setupOnCredentials();

    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    click('Sign in');

    expect(auth.login).toHaveBeenCalledWith({
      orgAlias: 'default',
      email: 'admin@dashboard.local',
      password: 'Password123!',
    });
  });

  it('submits autofilled credentials rather than empty strings', () => {
    setupOnCredentials();

    autofill('email', 'admin@dashboard.local');
    autofill('password', 'Password123!');
    click('Sign in');

    expect(auth.login).toHaveBeenCalledWith({
      orgAlias: 'default',
      email: 'admin@dashboard.local',
      password: 'Password123!',
    });
  });

  it('sends the organization from the route when the user landed on step 2 directly', () => {
    setup({}, { orgAlias: 'acme' });

    type('email', 'admin@dashboard.local');
    type('password', 'Password123!');
    click('Sign in');

    expect(auth.login).toHaveBeenCalledWith(
      jasmine.objectContaining({ orgAlias: 'acme' }),
    );
  });

  it('blames only the credentials when sign-in fails', () => {
    // The organization was already confirmed in step 1, so naming it again would misdirect.
    setupOnCredentials();
    auth.login.and.returnValue(throwError(() => ({ status: 401 })));

    type('email', 'admin@dashboard.local');
    type('password', 'wrong');
    click('Sign in');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid email or password.');
    expect(fixture.componentInstance.loading()).toBeFalse();
  });

  it('keeps the signals in step with what was submitted', () => {
    // Other parts of the template render from the signals; leaving them stale after an autofill
    // submit would show one thing while another was sent.
    setupOnCredentials();

    autofill('email', 'admin@dashboard.local');
    autofill('password', 'Password123!');
    click('Sign in');

    expect(fixture.componentInstance.email()).toBe('admin@dashboard.local');
    expect(fixture.componentInstance.orgAlias()).toBe('default');
  });

  // ── Going back ────────────────────────────────────────────────────────────

  it('returns to step 1 and clears the credentials when changing organization', () => {
    setupOnCredentials();
    type('email', 'admin@dashboard.local');

    click('Change');
    fixture.detectChanges();

    expect(fixture.componentInstance.step()).toBe('organization');
    expect(fixture.componentInstance.email()).toBe('');
    expect(router.navigate).toHaveBeenCalledWith(['/login'], jasmine.any(Object));
  });

  it('prefills the organization from the ?org= query parameter', () => {
    // Older invite links carry the tenant this way; the field must arrive populated.
    setup({ org: 'nexus' });

    expect(fixture.componentInstance.orgAlias()).toBe('nexus');
  });
});
