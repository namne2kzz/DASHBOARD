import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { SetPasswordPageComponent } from './set-password-page.component';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { LoginResponse } from '../../models/auth.model';

describe('SetPasswordPageComponent', () => {
  let fixture: ComponentFixture<SetPasswordPageComponent>;
  let component: SetPasswordPageComponent;
  let authSpy: jasmine.SpyObj<AuthService>;
  let repoCtxSpy: jasmine.SpyObj<RepositoryContextService>;
  let router: Router;

  const sessionStub: LoginResponse = {
    accessToken: 'access-token',
    jwtId: 'jwt-id',
    accessTokenExpiresAt: '2026-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAt: '2026-01-08T00:00:00Z',
    userId: 'user-1',
    name: 'Admin User',
    email: 'admin@acme.test',
    isGlobalAdmin: true,
    orgId: 'org-1',
    orgAlias: 'acme',
    avatarClass: 'bg-sky-500',
  };

  /** Builds the component with a given URL fragment, which is where the token lives. */
  async function createWithHash(hash: string): Promise<void> {
    window.location.hash = hash;

    authSpy    = jasmine.createSpyObj<AuthService>('AuthService', ['setPassword']);
    repoCtxSpy = jasmine.createSpyObj<RepositoryContextService>('RepositoryContextService', ['load']);

    await TestBed.configureTestingModule({
      imports: [SetPasswordPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: RepositoryContextService, useValue: repoCtxSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture   = TestBed.createComponent(SetPasswordPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => {
    window.location.hash = '';
    TestBed.resetTestingModule();
  });

  it('reports a missing token when the link carries no fragment', async () => {
    await createWithHash('');

    expect(component.state()).toBe('missing-token');
  });

  it('starts ready when a token is present', async () => {
    await createWithHash('#raw-token');

    expect(component.state()).toBe('ready');
  });

  it('marks each password rule as it is satisfied', async () => {
    await createWithHash('#raw-token');

    component.password.set('short');
    expect(component.allRulesMet()).toBeFalse();

    component.password.set('Str0ng!Pass');
    expect(component.rules().every(r => r.met)).toBeTrue();
  });

  it('keeps the rules neutral until the user leaves the field', async () => {
    // Mid-typing, an unmet rule is a step not yet reached — flagging it red would scold somebody
    // who is three characters into a password they were about to finish.
    await createWithHash('#raw-token');

    component.password.set('weak');
    expect(component.showRuleErrors()).toBeFalse();
  });

  it('flags the unmet rules once the field has been left with a bad password', async () => {
    await createWithHash('#raw-token');

    component.password.set('weak');
    component.passwordTouched.set(true);

    expect(component.showRuleErrors()).toBeTrue();
  });

  it('does not flag an empty field the user merely tabbed through', async () => {
    await createWithHash('#raw-token');

    component.passwordTouched.set(true);

    expect(component.showRuleErrors()).toBeFalse();
  });

  it('blocks submission until the confirmation matches', async () => {
    await createWithHash('#raw-token');
    component.password.set('Str0ng!Pass');

    component.confirm.set('Str0ng!Pas');
    expect(component.mismatch()).toBeTrue();
    expect(component.canSubmit()).toBeFalse();

    component.confirm.set('Str0ng!Pass');
    expect(component.mismatch()).toBeFalse();
    expect(component.canSubmit()).toBeTrue();
  });

  it('redeems the token and enters the app on success', async () => {
    await createWithHash('#raw-token');
    authSpy.setPassword.and.returnValue(of(sessionStub));

    component.password.set('Str0ng!Pass');
    component.confirm.set('Str0ng!Pass');
    component.submit();

    expect(authSpy.setPassword).toHaveBeenCalledWith('raw-token', 'Str0ng!Pass');
    expect(repoCtxSpy.load).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows the server message and allows another attempt when redeeming fails', async () => {
    await createWithHash('#raw-token');
    authSpy.setPassword.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400,
      error: { error: 'This link is invalid, expired or has already been used.' },
    })));

    component.password.set('Str0ng!Pass');
    component.confirm.set('Str0ng!Pass');
    component.submit();

    expect(component.errorMessage()).toBe('This link is invalid, expired or has already been used.');
    // Back to 'ready', not a terminal state: the user may retry after a transient failure.
    expect(component.state()).toBe('ready');
  });

  it('does not submit without a matching, valid password', async () => {
    await createWithHash('#raw-token');

    component.password.set('weak');
    component.confirm.set('weak');
    component.submit();

    expect(authSpy.setPassword).not.toHaveBeenCalled();
  });
});
