import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { SettingsMetadataPageComponent } from './settings-metadata-page.component';
import { MetadataService } from '../../services/metadata.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { RepositoryContextService } from '../../services/repository-context.service';

describe('SettingsMetadataPageComponent', () => {
  let component: SettingsMetadataPageComponent;
  let fixture: ComponentFixture<SettingsMetadataPageComponent>;

  const metadataStub = {
    items:   signal([]),
    keys:    signal([]),
    loading: signal(false),
    error:   signal<string | null>(null),
    create:  jasmine.createSpy('create').and.returnValue(of(void 0)),
    update:  jasmine.createSpy('update').and.returnValue(of(void 0)),
    remove:  jasmine.createSpy('remove').and.returnValue(of(void 0)),
  } as unknown as Partial<MetadataService> & { create: jasmine.Spy };

  // Signals rather than fixed functions so a test can revoke a permission and re-ask.
  const canManageMetadata = signal(true);
  const isGlobalAdmin     = signal(true);

  const privilegeStub: Partial<PrivilegeService> = {
    canManageMetadata,
    canManageMembers: signal(true),
    isGlobalAdmin,
  } as unknown as Partial<PrivilegeService>;

  const repoCtxStub: Partial<RepositoryContextService> = {
    selectedRepoId: () => 'repo-1',
    selectedRepo:   () => null,
  } as unknown as Partial<RepositoryContextService>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsMetadataPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MetadataService,          useValue: metadataStub },
        { provide: PrivilegeService,         useValue: privilegeStub },
        { provide: RepositoryContextService, useValue: repoCtxStub },
      ],
    }).compileComponents();

    canManageMetadata.set(true);
    isGlobalAdmin.set(true);
    metadataStub.create.calls.reset();

    fixture   = TestBed.createComponent(SettingsMetadataPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should open and close the create panel', () => {
    component.openCreate();
    expect(component.activePanel()).toBe('create');
    component.closePanel();
    expect(component.activePanel()).toBeNull();
  });

  describe('permission gating', () => {
    const repoEntry   = { id: 'm1', key: 'k', value: 'v', displayName: 'Key', isGlobal: false } as never;
    const globalEntry = { id: 'm2', key: 'k', value: 'v', displayName: 'Key', isGlobal: true  } as never;

    it('gates repository entries on ManageMetadata, not on member management', () => {
      // The API's metadata commands check ManageMetadata; this page used to ask for ManageMembers,
      // which both over- and under-granted depending on the role.
      expect(component.canManage(repoEntry)).toBeTrue();

      canManageMetadata.set(false);
      isGlobalAdmin.set(false);

      expect(component.canManage(repoEntry)).toBeFalse();
      expect(component.canCreate()).toBeFalse();
    });

    it('still requires a global admin for a global entry', () => {
      isGlobalAdmin.set(false);

      expect(component.canManage(globalEntry)).toBeFalse();
      expect(component.canManage(repoEntry)).toBeTrue();
    });

    it('refuses to open the create panel without the permission', () => {
      canManageMetadata.set(false);
      isGlobalAdmin.set(false);

      component.openCreate();

      expect(component.activePanel()).toBeNull();
    });

    it('refuses to submit without the permission, even with a filled form', () => {
      // submit() was the one mutating handler with no guard of its own; reaching it needs only a
      // panel left open from before the permission changed.
      component.openCreate();
      component.formValue.set('something');
      canManageMetadata.set(false);
      isGlobalAdmin.set(false);

      component.submit();

      expect(metadataStub.create).not.toHaveBeenCalled();
    });
  });
});
