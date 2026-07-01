import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';

import { SettingsMetadataPageComponent } from './settings-metadata-page.component';
import { MetadataService } from '../../services/metadata.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { RepositoryContextService } from '../../services/repository-context.service';

describe('SettingsMetadataPageComponent', () => {
  let component: SettingsMetadataPageComponent;
  let fixture: ComponentFixture<SettingsMetadataPageComponent>;

  const metadataStub: Partial<MetadataService> = {
    items:   signal([]),
    keys:    signal([]),
    loading: signal(false),
    error:   signal<string | null>(null),
  } as unknown as Partial<MetadataService>;

  const privilegeStub: Partial<PrivilegeService> = {
    canManageMembers: () => true,
    isGlobalAdmin:    () => true,
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
});
