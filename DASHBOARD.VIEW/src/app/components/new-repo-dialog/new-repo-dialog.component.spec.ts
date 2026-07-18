import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { NewRepoDialogComponent } from './new-repo-dialog.component';
import { RepositoryContextService } from '../../services/repository-context.service';
import { SystemUsersService } from '../../services/system-users.service';
import { SystemUserDto, AuthProvider } from '../../models/system-user.model';
import { RepositoryApiDto } from '../../models/repository.model';

const mockUser: SystemUserDto = {
  userId: 'user-1',
  email: 'alice@test.com',
  name: 'Alice',
  avatarClass: 'bg-sky-600',
  isGlobalAdmin: false,
  isActive: true,
  authProvider: AuthProvider.System,
  createdAt: '2024-01-01T00:00:00Z',
  lastLoginAt: null,
  repoMemberships: [],
};

const mockRepo: RepositoryApiDto = {
  id: 'repo-1',
  name: 'Test Repo',
  code: 'TEST',
  description: '',
  memberCount: 1,
  createdAt: '2024-01-01T00:00:00Z',
};

const mockDialogRef = { close: jasmine.createSpy('close') };

describe('NewRepoDialogComponent', () => {
  let fixture: ComponentFixture<NewRepoDialogComponent>;
  let component: NewRepoDialogComponent;
  let repoCtx: jasmine.SpyObj<Pick<RepositoryContextService, 'create'>>;
  let usersSvc: { users: ReturnType<typeof signal<SystemUserDto[]>>; loading: ReturnType<typeof signal<boolean>>; load: jasmine.Spy };

  beforeEach(async () => {
    repoCtx  = { create: jasmine.createSpy('create').and.returnValue(of(mockRepo)) };
    usersSvc = {
      users:   signal<SystemUserDto[]>([mockUser]),
      loading: signal(false),
      load:    jasmine.createSpy('load'),
    };

    await TestBed.configureTestingModule({
      imports: [NewRepoDialogComponent],
      providers: [
        { provide: RepositoryContextService, useValue: repoCtx },
        { provide: SystemUsersService,        useValue: usersSvc },
        { provide: 'DIALOG_REF',              useValue: mockDialogRef },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(NewRepoDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('does not load users when already populated', () => {
    expect(usersSvc.load).not.toHaveBeenCalled();
  });

  it('calls load() when users list is empty on init', async () => {
    usersSvc.users.set([]);
    await TestBed.createComponent(NewRepoDialogComponent).ngOnInit();
    expect(usersSvc.load).toHaveBeenCalled();
  });

  it('uppercases and strips invalid chars from code field', () => {
    component.code = 'my-code!';
    component.onCodeInput();
    expect(component.code).toBe('MYCODE');
  });

  it('calls create and closes dialog on success', () => {
    component.name          = 'Test Repo';
    component.code          = 'TEST';
    component.description   = '';
    component.scrumMasterId = 'user-1';
    component.submit();
    expect(repoCtx.create).toHaveBeenCalledWith({
      name: 'Test Repo', code: 'TEST', description: '', scrumMasterId: 'user-1',
    });
    expect(mockDialogRef.close).toHaveBeenCalledWith(mockRepo);
  });

  it('sets error message on API failure', () => {
    repoCtx.create.and.returnValue(throwError(() => ({ error: { detail: 'Code already exists.' } })));
    component.name = 'X'; component.code = 'X'; component.scrumMasterId = 'user-1';
    component.submit();
    expect(component.error()).toBe('Code already exists.');
  });

  it('cancel() closes dialog without a result', () => {
    mockDialogRef.close.calls.reset();
    component.cancel();
    expect(mockDialogRef.close).toHaveBeenCalledWith(undefined);
  });
});
