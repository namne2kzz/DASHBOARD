import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { SettingsMembersPageComponent } from './settings-members-page.component';
import { PrivilegeService } from '../../core/services/privilege.service';
import { MembersService } from '../../services/members.service';
import { RoleService } from '../../services/role.service';
import { MetadataService } from '../../services/metadata.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { Permission } from '../../core/enums/system.enum';

describe('SettingsMembersPageComponent', () => {
  let component: SettingsMembersPageComponent;
  let fixture: ComponentFixture<SettingsMembersPageComponent>;

  const privilegeStub: Partial<PrivilegeService> = {
    canManageMembers:    () => true,
    canManageRoles:      () => true,
    canActOnMember:      () => true,
    canInviteMembers:    () => true,
  } as unknown as Partial<PrivilegeService>;

  const membersStub: Partial<MembersService> = {
    members:  signal([]),
    initials: (name: string) => name.slice(0, 2).toUpperCase(),
    getMember: () => null,
  } as unknown as Partial<MembersService>;

  const roleStub: Partial<RoleService> = {
    roles: signal([]),
  } as unknown as Partial<RoleService>;

  const metadataStub: Partial<MetadataService> = {
    items: signal([]),
    keys:  signal([]),
  } as unknown as Partial<MetadataService>;

  const toastStub: Partial<ToastService> = {
    warning: () => undefined,
    error:   () => undefined,
  } as unknown as Partial<ToastService>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsMembersPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PrivilegeService, useValue: privilegeStub },
        { provide: MembersService,   useValue: membersStub },
        { provide: RoleService,      useValue: roleStub },
        { provide: MetadataService,  useValue: metadataStub },
        { provide: ToastService,     useValue: toastStub },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(SettingsMembersPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should open and close panels', () => {
    component.openPanel('add-member');
    expect(component.activePanel()).toBe('add-member');
    component.closePanel();
    expect(component.activePanel()).toBeNull();
  });

  it('should toggle permissions in the role form', () => {
    component.openPanel('create-role');
    expect(component.hasPermission(Permission.CreateWorkItem)).toBeFalse();
    component.togglePermission(Permission.CreateWorkItem);
    expect(component.hasPermission(Permission.CreateWorkItem)).toBeTrue();
    component.togglePermission(Permission.CreateWorkItem);
    expect(component.hasPermission(Permission.CreateWorkItem)).toBeFalse();
  });

  it('should filter members by search query', () => {
    component.searchQuery.set('nonexistent@test.com');
    expect(component.filteredMembers().length).toBe(0);
  });
});
