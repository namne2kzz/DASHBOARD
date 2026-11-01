import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { SettingsRepositoriesPageComponent } from './settings-repositories-page.component';
import { RepositoryAdminService } from '../../services/repository-admin.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { RepositoryApiDto } from '../../models/repository.model';
import { MemberApiDto } from '../../models/member.model';

describe('SettingsRepositoriesPageComponent', () => {
  let fixture: ComponentFixture<SettingsRepositoriesPageComponent>;
  let component: SettingsRepositoriesPageComponent;
  let svc: jasmine.SpyObj<RepositoryAdminService> & { repositories: ReturnType<typeof signal<RepositoryApiDto[]>> };
  let repoCtx: jasmine.SpyObj<RepositoryContextService>;
  let toast: jasmine.SpyObj<ToastService>;

  function repo(over: Partial<RepositoryApiDto> = {}): RepositoryApiDto {
    return {
      id: 'r1', name: 'Platform', code: 'PLAT', description: 'Core services',
      memberCount: 4, createdAt: '2026-01-01T00:00:00Z',
      isArchived: false, leadId: 'u1', leadName: 'Alice',
      ...over,
    };
  }

  const members: MemberApiDto[] = [
    { memberId: 'm1', userId: 'u1', userName: 'Alice', userEmail: 'alice@test.com',
      avatarClass: 'bg-sky-600', defaultRole: 'Project Manager', roleId: 'role-pm', roleName: null, joinedAt: '' },
    { memberId: 'm2', userId: 'u2', userName: 'Bob', userEmail: 'bob@test.com',
      avatarClass: 'bg-rose-600', defaultRole: 'Developer', roleId: 'role-dev', roleName: null, joinedAt: '' },
  ];

  function setup(repos: RepositoryApiDto[] = [repo()]): void {
    const repositories = signal<RepositoryApiDto[]>(repos);

    svc = Object.assign(
      jasmine.createSpyObj<RepositoryAdminService>('RepositoryAdminService',
        ['load', 'update', 'archive', 'unarchive', 'members']),
      { repositories, loading: signal(false), error: signal<string | null>(null) },
    ) as typeof svc;

    svc.members.and.returnValue(of(members));
    svc.update.and.returnValue(of(void 0));
    svc.archive.and.returnValue(of(void 0));
    svc.unarchive.and.returnValue(of(void 0));

    // The page now reads PrivilegeService, which pulls in MembersService; that service's
    // constructor effect reads the selected repository, so the stub has to answer it.
    repoCtx = jasmine.createSpyObj<RepositoryContextService>(
      'RepositoryContextService',
      ['load'],
      { selectedRepoId: signal(null), selectedRepo: signal(null) },
    );
    toast   = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info']);

    TestBed.configureTestingModule({
      imports: [SettingsRepositoriesPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Repository administration is global-admin only; these tests are about what the page
        // does once that is granted, so the privilege is stubbed rather than re-asserted here.
        { provide: PrivilegeService,          useValue: { isGlobalAdmin: signal(true) } },
        { provide: RepositoryAdminService,    useValue: svc },
        { provide: RepositoryContextService,  useValue: repoCtx },
        { provide: ToastService,              useValue: toast },
        { provide: DialogService,             useValue: jasmine.createSpyObj('DialogService', ['open']) },
      ],
    });

    fixture   = TestBed.createComponent(SettingsRepositoriesPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  it('loads every repository, archived ones included', () => {
    setup();

    expect(svc.load).toHaveBeenCalled();
  });

  // ── Filtering ─────────────────────────────────────────────────

  it('hides archived repositories by default', () => {
    setup([repo({ id: 'r1' }), repo({ id: 'r2', name: 'Legacy', isArchived: true })]);

    expect(component.activeFilter()).toBe('active');
    expect(component.filteredRepos().map(r => r.id)).toEqual(['r1']);
  });

  it('shows only archived repositories on the archived tab', () => {
    setup([repo({ id: 'r1' }), repo({ id: 'r2', name: 'Legacy', isArchived: true })]);

    component.activeFilter.set('archived');

    expect(component.filteredRepos().map(r => r.id)).toEqual(['r2']);
  });

  it('counts each slice separately', () => {
    setup([repo({ id: 'r1' }), repo({ id: 'r2', isArchived: true }), repo({ id: 'r3' })]);

    expect(component.counts()).toEqual({ all: 3, active: 2, archived: 1 });
  });

  it('matches the search against name and code', () => {
    setup([repo({ id: 'r1', name: 'Platform', code: 'PLAT' }),
           repo({ id: 'r2', name: 'Billing',  code: 'BILL' })]);

    component.searchQuery.set('bill');
    expect(component.filteredRepos().map(r => r.id)).toEqual(['r2']);

    component.searchQuery.set('PLAT');
    expect(component.filteredRepos().map(r => r.id)).toEqual(['r1']);
  });

  // ── Editing ───────────────────────────────────────────────────

  it('fills the form from the repository being edited', () => {
    setup();

    component.openEdit(repo());

    expect(component.formName()).toBe('Platform');
    expect(component.formDescription()).toBe('Core services');
    expect(component.formLead()).toBe('u1');
    expect(svc.members).toHaveBeenCalledWith('r1');
  });

  it('offers only that repository\'s members as Project Manager', () => {
    setup();

    component.openEdit(repo());

    expect(component.memberOptions().map(o => o.id)).toEqual(['u1', 'u2']);
  });

  it('cannot save until something changes', () => {
    setup();
    component.openEdit(repo());

    expect(component.canSave()).toBeFalse();

    component.formName.set('Platform Core');
    expect(component.canSave()).toBeTrue();
  });

  it('cannot save an empty name', () => {
    setup();
    component.openEdit(repo());

    component.formName.set('   ');

    expect(component.canSave()).toBeFalse();
  });

  it('leaves the Project Manager alone when only the name changed', () => {
    // Sending the unchanged holder would make a rename look like a handover in the audit trail.
    setup();
    component.openEdit(repo());

    component.formName.set('Platform Core');
    component.save();

    expect(svc.update).toHaveBeenCalledWith('r1', {
      name: 'Platform Core',
      description: 'Core services',
      leadId: undefined,
    });
  });

  it('sends the new Project Manager when it is handed over', () => {
    setup();
    component.openEdit(repo());

    component.formLead.set('u2');
    component.save();

    expect(svc.update).toHaveBeenCalledWith('r1', jasmine.objectContaining({ leadId: 'u2' }));
  });

  it('refreshes the sidebar after a rename, since it shows repository names', () => {
    setup();
    component.openEdit(repo());
    component.formName.set('Platform Core');

    component.save();

    expect(repoCtx.load).toHaveBeenCalled();
  });

  it('keeps the form open and reports when saving fails', () => {
    setup();
    component.openEdit(repo());
    component.formName.set('Platform Core');
    svc.update.and.returnValue(throwError(() => new Error('boom')));

    component.save();

    expect(component.editing()).not.toBeNull();
    expect(component.saving()).toBeFalse();
    expect(toast.error).toHaveBeenCalled();
  });

  // ── Archive / restore ─────────────────────────────────────────

  it('asks before archiving', () => {
    setup();

    component.confirmArchive(repo());

    expect(component.archiving()?.id).toBe('r1');
    expect(svc.archive).not.toHaveBeenCalled();
  });

  it('will not archive until the repository code is typed', () => {
    // The safeguard: a prompt opened on the wrong repository cannot be cleared by reflex, because
    // the code can only be produced by reading which repository is named.
    setup();
    component.confirmArchive(repo());

    expect(component.canArchive()).toBeFalse();

    component.archiveConfirmation.set('PLA');
    expect(component.canArchive()).toBeFalse();

    component.archive();
    expect(svc.archive).not.toHaveBeenCalled();
  });

  it('rejects another repository\'s code', () => {
    setup();
    component.confirmArchive(repo({ code: 'PLAT' }));

    component.archiveConfirmation.set('BILL');

    expect(component.canArchive()).toBeFalse();
  });

  it('accepts the code whatever the casing, and ignores surrounding spaces', () => {
    setup();
    component.confirmArchive(repo({ code: 'PLAT' }));

    component.archiveConfirmation.set('  plat ');

    expect(component.canArchive()).toBeTrue();
  });

  it('archives once the code matches', () => {
    setup();
    component.confirmArchive(repo());

    component.archiveConfirmation.set('PLAT');
    component.archive();

    expect(svc.archive).toHaveBeenCalledWith('r1');
    expect(component.archiving()).toBeNull();
    // Archived repositories must also vanish from the sidebar switcher.
    expect(repoCtx.load).toHaveBeenCalled();
  });

  it('clears what was typed when the prompt is dismissed', () => {
    // Otherwise the next prompt would open already satisfied, from a confirmation meant for a
    // different repository.
    setup();
    component.confirmArchive(repo());
    component.archiveConfirmation.set('PLAT');

    component.cancelArchive();

    expect(component.archiveConfirmation()).toBe('');
    component.archive();
    expect(svc.archive).not.toHaveBeenCalled();
  });

  it('starts each prompt with an empty confirmation', () => {
    setup();
    component.confirmArchive(repo());
    component.archiveConfirmation.set('PLAT');
    component.cancelArchive();

    component.confirmArchive(repo({ id: 'r2', code: 'BILL' }));

    expect(component.archiveConfirmation()).toBe('');
    expect(component.canArchive()).toBeFalse();
  });

  it('restores an archived repository', () => {
    setup([repo({ isArchived: true })]);

    component.unarchive(repo({ isArchived: true }));

    expect(svc.unarchive).toHaveBeenCalledWith('r1');
    expect(repoCtx.load).toHaveBeenCalled();
  });
});
