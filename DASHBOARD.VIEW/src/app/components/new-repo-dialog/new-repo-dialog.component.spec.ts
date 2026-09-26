import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { NewRepoDialogComponent } from './new-repo-dialog.component';
import { RepositoryContextService } from '../../services/repository-context.service';
import { SystemUsersService } from '../../services/system-users.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { UserPickerItem, UserProfile } from '../../models/user.model';
import { RepositoryApiDto } from '../../models/repository.model';

const mockProfile: UserProfile = {
  userId:        'user-1',
  email:         'alice@test.com',
  name:          'Alice',
  avatarClass:   'bg-sky-600',
  isGlobalAdmin: false,
  orgId:         'org-1',
  orgAlias:      'acme',
};

const mockPickerItem: UserPickerItem = {
  userId:      'user-2',
  name:        'Bob Nguyen',
  email:       'bob@test.com',
  avatarClass: 'bg-emerald-600',
};

const mockRepo: RepositoryApiDto = {
  id:          'repo-1',
  name:        'Test Repo',
  code:        'TEST',
  description: '',
  memberCount: 1,
  createdAt:   '2024-01-01T00:00:00Z',
};

describe('NewRepoDialogComponent', () => {
  let fixture:   ComponentFixture<NewRepoDialogComponent>;
  let component: NewRepoDialogComponent;
  let repoCtx:   jasmine.SpyObj<Pick<RepositoryContextService, 'create' | 'checkCode'>>;
  let usersSvc:  jasmine.SpyObj<Pick<SystemUsersService, 'searchAllUsers'>>;
  let toast:     jasmine.SpyObj<Pick<ToastService, 'error'>>;
  let dialogRef: jasmine.SpyObj<{ close(result?: unknown): void }>;

  beforeEach(async () => {
    repoCtx = jasmine.createSpyObj<Pick<RepositoryContextService, 'create' | 'checkCode'>>(
      'RepositoryContextService', ['create', 'checkCode']);
    repoCtx.create.and.returnValue(of(mockRepo));
    repoCtx.checkCode.and.returnValue(of(true));

    usersSvc = jasmine.createSpyObj<Pick<SystemUsersService, 'searchAllUsers'>>(
      'SystemUsersService', ['searchAllUsers']);
    usersSvc.searchAllUsers.and.returnValue(of([mockPickerItem]));

    toast     = jasmine.createSpyObj<Pick<ToastService, 'error'>>('ToastService', ['error']);
    dialogRef = jasmine.createSpyObj<{ close(result?: unknown): void }>('DialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [NewRepoDialogComponent],
      providers: [
        { provide: RepositoryContextService, useValue: repoCtx },
        { provide: SystemUsersService,       useValue: usersSvc },
        { provide: ToastService,             useValue: toast },
        { provide: DIALOG_REF_TOKEN,         useValue: dialogRef },
        { provide: AuthService,              useValue: { currentUser: signal<UserProfile | null>(mockProfile) } },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(NewRepoDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('exposes the caller org alias', () => {
    expect(component.orgAlias).toBe('acme');
  });

  // ── Code field ──────────────────────────────────────────────

  it('uppercases and strips invalid chars from the code field', () => {
    component.code = 'my-code!';
    component.onCodeInput();
    expect(component.code).toBe('MYCODE');
  });

  it('resets code status to idle while typing', () => {
    component.codeStatus.set('taken');
    component.code = 'AB';
    component.onCodeInput();
    expect(component.codeStatus()).toBe('idle');
  });

  it('marks the code available when the API says so', () => {
    component.code = 'TEST';
    component.onCodeBlur();
    expect(repoCtx.checkCode).toHaveBeenCalledWith('TEST');
    expect(component.codeStatus()).toBe('available');
  });

  it('marks the code taken when already in use', () => {
    repoCtx.checkCode.and.returnValue(of(false));
    component.code = 'TEST';
    component.onCodeBlur();
    expect(component.codeStatus()).toBe('taken');
  });

  it('stays idle when the code field is blank on blur', () => {
    component.code = '   ';
    component.onCodeBlur();
    expect(repoCtx.checkCode).not.toHaveBeenCalled();
    expect(component.codeStatus()).toBe('idle');
  });

  // ── Scrum Master picker ─────────────────────────────────────

  it('searches users after the debounce window', fakeAsync(() => {
    const el = document.createElement('input');
    component.onUserSearch('bo', el);
    expect(component.searching()).toBeTrue();
    tick(400);
    expect(usersSvc.searchAllUsers).toHaveBeenCalledWith('bo');
    expect(component.searchResults()).toEqual([mockPickerItem]);
    expect(component.searching()).toBeFalse();
  }));

  it('does not search for terms shorter than 2 characters', fakeAsync(() => {
    const el = document.createElement('input');
    component.onUserSearch('b', el);
    tick(400);
    expect(usersSvc.searchAllUsers).not.toHaveBeenCalled();
    expect(component.searchResults()).toEqual([]);
  }));

  it('selectUser() stores the pick and clears the result list', () => {
    component.searchResults.set([mockPickerItem]);
    component.selectUser(mockPickerItem);
    expect(component.selectedUser()).toEqual(mockPickerItem);
    expect(component.searchResults()).toEqual([]);
    expect(component.searchTerm()).toBe('');
  });

  it('clearUser() resets the picker', () => {
    component.selectedUser.set(mockPickerItem);
    component.clearUser();
    expect(component.selectedUser()).toBeNull();
    expect(component.dropdownRect()).toBeNull();
  });

  it('builds initials from a full name', () => {
    expect(component.initials('Bob Nguyen')).toBe('BN');
    expect(component.initials('Alice')).toBe('AL');
    expect(component.initials('   ')).toBe('?');
  });

  // ── Submit ──────────────────────────────────────────────────

  it('calls create and closes the dialog on success', () => {
    component.name        = 'Test Repo';
    component.code        = 'TEST';
    component.description = '';
    component.selectedUser.set(mockPickerItem);

    component.submit();

    expect(repoCtx.create).toHaveBeenCalledWith({
      name: 'Test Repo', code: 'TEST', description: '', scrumMasterId: 'user-2',
    });
    expect(dialogRef.close).toHaveBeenCalledWith(mockRepo);
    expect(component.submitting()).toBeFalse();
  });

  it('does not submit without a selected Scrum Master', () => {
    component.name = 'Test Repo';
    component.code = 'TEST';
    component.submit();
    expect(repoCtx.create).not.toHaveBeenCalled();
  });

  it('does not submit when the code is already taken', () => {
    component.name = 'Test Repo';
    component.code = 'TEST';
    component.selectedUser.set(mockPickerItem);
    component.codeStatus.set('taken');
    component.submit();
    expect(repoCtx.create).not.toHaveBeenCalled();
  });

  it('shows a toast with the API detail on failure', () => {
    repoCtx.create.and.returnValue(throwError(() => ({ error: { detail: 'Code already exists.' } })));
    component.name = 'X';
    component.code = 'X';
    component.selectedUser.set(mockPickerItem);

    component.submit();

    expect(toast.error).toHaveBeenCalledWith('Code already exists.');
    expect(component.submitting()).toBeFalse();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('falls back to a generic message when the API gives no detail', () => {
    repoCtx.create.and.returnValue(throwError(() => ({})));
    component.name = 'X';
    component.code = 'X';
    component.selectedUser.set(mockPickerItem);

    component.submit();

    expect(toast.error).toHaveBeenCalledWith('Failed to create repository.');
  });

  it('cancel() closes the dialog without a result', () => {
    component.cancel();
    expect(dialogRef.close).toHaveBeenCalledWith();
  });
});
