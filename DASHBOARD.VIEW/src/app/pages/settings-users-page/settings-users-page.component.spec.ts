import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { SettingsUsersPageComponent } from './settings-users-page.component';
import { SystemUsersService } from '../../services/system-users.service';
import { AuthService } from '../../services/auth.service';
import { SystemUserDto, AuthProvider } from '../../models/system-user.model';
import { DateTimeService } from '../../core/services/date-time.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { ToastService } from '../../core/components/toast/toast.service';

const mockUser = (overrides: Partial<SystemUserDto> = {}): SystemUserDto => ({
  userId:          'u1',
  email:           'test@example.com',
  name:            'Test User',
  avatarClass:     'bg-sky-600',
  isGlobalAdmin:   false,
  isActive:        true,
  authProvider:    AuthProvider.System,
  createdAt:       '2024-01-01T00:00:00Z',
  lastLoginAt:     null,
  managerId:       null,
  managerName:     null,
  repoMemberships: [],
  ...overrides,
});

describe('SettingsUsersPageComponent', () => {
  let component: SettingsUsersPageComponent;
  let fixture: ComponentFixture<SettingsUsersPageComponent>;

  const svcStub: Partial<SystemUsersService> = {
    users:    signal<SystemUserDto[]>([]),
    loading:  signal(false),
    error:    signal(null),
    load:     jasmine.createSpy('load'),
    initials: (name: string) => name.slice(0, 2).toUpperCase(),
  } as unknown as Partial<SystemUsersService>;

  const authStub: Partial<AuthService> = {
    currentUser: signal({ userId: 'me', email: 'admin@test.com', name: 'Admin', avatarClass: '', isGlobalAdmin: true }),
  } as unknown as Partial<AuthService>;

  // DateTimeService and PrivilegeService pull in PreferencesService / MembersService /
  // RoleService transitively — stub them so the page under test stays isolated.
  const dtStub = {
    format:   (utc: string | null | undefined) => utc ?? '',
    relative: (utc: string | null | undefined) => utc ?? '',
  } as unknown as DateTimeService;

  const privilegeStub = {
    isGlobalAdmin:     signal(true),
    canManageMembers:  signal(true),
  } as unknown as PrivilegeService;

  const toastStub = jasmine.createSpyObj<ToastService>(
    'ToastService', ['success', 'error', 'warning', 'info']);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsUsersPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SystemUsersService, useValue: svcStub },
        { provide: AuthService,        useValue: authStub },
        { provide: DateTimeService,    useValue: dtStub },
        { provide: PrivilegeService,   useValue: privilegeStub },
        { provide: ToastService,       useValue: toastStub },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(SettingsUsersPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and call load on init', () => {
    expect(component).toBeTruthy();
    expect(svcStub.load).toHaveBeenCalled();
  });

  it('should open and close the create modal', () => {
    component.openCreateModal();
    expect(component.showCreateModal()).toBeTrue();
    component.closeCreateModal();
    expect(component.showCreateModal()).toBeFalse();
  });

  it('should detect password mismatch', () => {
    component.formPassword.set('password123');
    component.formConfirm.set('different');
    expect(component.passwordMismatch()).toBeTrue();
    component.formConfirm.set('password123');
    expect(component.passwordMismatch()).toBeFalse();
  });

  it('should block acting on self', () => {
    const self = mockUser({ userId: 'me' });
    expect(component.canActOnUser(self)).toBeFalse();
  });

  it('should allow acting on other users', () => {
    const other = mockUser({ userId: 'other' });
    expect(component.canActOnUser(other)).toBeTrue();
  });

  it('should toggle row expansion', () => {
    expect(component.isExpanded('u1')).toBeFalse();
    component.toggleExpand('u1');
    expect(component.isExpanded('u1')).toBeTrue();
    component.toggleExpand('u1');
    expect(component.isExpanded('u1')).toBeFalse();
  });
});
