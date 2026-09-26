import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { InviteAcceptPageComponent } from './invite-accept-page.component';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { InvitationsService } from '../../services/invitations.service';
import { AuthService } from '../../services/auth.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { LoginResponse } from '../../models/auth.model';

describe('InviteAcceptPageComponent', () => {
  let fixture: ComponentFixture<InviteAcceptPageComponent>;
  let component: InviteAcceptPageComponent;
  let googleIdentitySpy: jasmine.SpyObj<GoogleIdentityService>;
  let invitationsSpy: jasmine.SpyObj<InvitationsService>;
  let authSpy: jasmine.SpyObj<AuthService>;
  let repoCtxSpy: jasmine.SpyObj<RepositoryContextService>;

  const sessionStub: LoginResponse = {
    accessToken: 'access-token',
    jwtId: 'jwt-id',
    accessTokenExpiresAt: '2026-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAt: '2026-01-08T00:00:00Z',
    userId: 'user-1',
    name: 'Test User',
    email: 'invitee@test.com',
    isGlobalAdmin: false,
    orgId: 'org-1',
    orgAlias: 'acme',
    avatarClass: 'bg-sky-600',
  };

  beforeEach(async () => {
    googleIdentitySpy = jasmine.createSpyObj('GoogleIdentityService', ['renderSignInButton']);
    invitationsSpy    = jasmine.createSpyObj('InvitationsService', ['accept']);
    authSpy           = jasmine.createSpyObj('AuthService', ['applySession']);
    repoCtxSpy        = jasmine.createSpyObj('RepositoryContextService', ['load']);

    await TestBed.configureTestingModule({
      imports: [InviteAcceptPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: GoogleIdentityService, useValue: googleIdentitySpy },
        { provide: InvitationsService, useValue: invitationsSpy },
        { provide: AuthService, useValue: authSpy },
        { provide: RepositoryContextService, useValue: repoCtxSpy },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    window.location.hash = '';
  });

  function create(hash: string): void {
    window.location.hash = hash;
    fixture = TestBed.createComponent(InviteAcceptPageComponent);
    component = fixture.componentInstance;
  }

  it('should create', () => {
    googleIdentitySpy.renderSignInButton.and.returnValue(new Promise(() => {}));
    create('some-token');
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('shows an invalid-link state when the URL has no token fragment', () => {
    create('');
    fixture.detectChanges();
    expect(component.state()).toBe('missing-token');
    expect(googleIdentitySpy.renderSignInButton).not.toHaveBeenCalled();
  });

  it('accepts the invite and starts a session on successful Google sign-in', done => {
    googleIdentitySpy.renderSignInButton.and.returnValue(Promise.resolve('google-id-token'));
    invitationsSpy.accept.and.returnValue(of(sessionStub));
    create('abc123');
    fixture.detectChanges();

    setTimeout(() => {
      expect(invitationsSpy.accept).toHaveBeenCalledWith('abc123', 'google-id-token');
      expect(authSpy.applySession).toHaveBeenCalledWith(sessionStub);
      expect(repoCtxSpy.load).toHaveBeenCalled();
      done();
    });
  });

  it('shows the backend error message when accept fails', done => {
    googleIdentitySpy.renderSignInButton.and.returnValue(Promise.resolve('google-id-token'));
    invitationsSpy.accept.and.returnValue(throwError(() => ({ error: { error: 'Invitation has expired.' } })));
    create('abc123');
    fixture.detectChanges();

    setTimeout(() => {
      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toBe('Invitation has expired.');
      done();
    });
  });

  it('shows a generic error message when Google Sign-In fails to load', done => {
    googleIdentitySpy.renderSignInButton.and.returnValue(Promise.reject(new Error('Google Sign-In is unavailable right now. Please refresh and try again.')));
    create('abc123');
    fixture.detectChanges();

    setTimeout(() => {
      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toBe('Google Sign-In is unavailable right now. Please refresh and try again.');
      done();
    });
  });
});
