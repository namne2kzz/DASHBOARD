import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MembersService } from '../../services/members.service';
import { RoleService } from '../../services/role.service';
import { MetadataService } from '../../services/metadata.service';
import { InvitationsService } from '../../services/invitations.service';
import { ConfirmService } from '../../core/components/confirm/confirm.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { DateTimeService } from '../../core/services/date-time.service';
import { MemberApiDto } from '../../models/member.model';
import { UserPickerItem } from '../../models/user.model';
import { RoleDto, CreateRolePayload, UpdateRolePayload } from '../../models/role.model';
import { InvitationListItemDto, InvitationStatus } from '../../models/invitation.model';
import { Permission } from '../../core/enums/system.enum';
import { PERMISSION_LABELS } from '../../core/constants/system.constant';
import { UserSelectComponent, UserOption } from '../../components/user-select/user-select.component';

type ActivePanel = 'add-member' | 'invite-email' | 'create-role' | 'edit-role' | null;

@Component({
  selector: 'app-settings-members-page',
  standalone: true,
  imports: [FormsModule, NgClass, UserSelectComponent],
  templateUrl: './settings-members-page.component.html',
  styleUrls: ['./settings-members-page.component.css'],
})
export class SettingsMembersPageComponent {
  protected readonly membersService     = inject(MembersService);
  protected readonly roleService        = inject(RoleService);
  protected readonly metadataService    = inject(MetadataService);
  protected readonly invitationsService = inject(InvitationsService);
  private  readonly confirm             = inject(ConfirmService);
  private  readonly toast               = inject(ToastService);
  protected readonly privilege          = inject(PrivilegeService);
  protected readonly repoCtx            = inject(RepositoryContextService);
  protected readonly dt                 = inject(DateTimeService);

  protected readonly PERMISSION_LABELS = PERMISSION_LABELS;
  protected readonly InvitationStatus  = InvitationStatus;
  // Numeric enum: Object.values yields both names and numbers — keep only the numeric values.
  protected readonly ALL_PERMISSIONS   = Object.values(Permission).filter((v): v is Permission => typeof v === 'number');

  /** Team-role (discipline) options loaded from RepoRole metadata of the active repo. */
  readonly disciplines = computed(() =>
    this.metadataService.items().filter(m => m.key === 'RepoRole').map(m => m.value),
  );

  /** The seeded Scrum Master discipline value, guarded as the repository's lead discipline. */
  private readonly SCRUM_MASTER = 'Scrum Master';

  /** Id of the default Scrum Master permission role, when loaded. */
  private readonly scrumMasterRoleId = computed(() =>
    this.roleService.roles().find(r => r.isDefault && r.name === this.SCRUM_MASTER)?.id ?? null,
  );

  /** Number of members whose discipline is Scrum Master. */
  private readonly smDisciplineCount = computed(() =>
    this.membersService.members().filter(m => m.defaultRole === this.SCRUM_MASTER).length,
  );

  /** Number of members holding the Scrum Master role. */
  private readonly smRoleCount = computed(() =>
    this.membersService.members().filter(m => m.roleId === this.scrumMasterRoleId()).length,
  );

  // ── Search ────────────────────────────────────────────────────
  readonly searchQuery = signal('');

  readonly filteredMembers = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.membersService.members();
    return this.membersService.members().filter(m =>
      m.userName.toLowerCase().includes(q) || m.userEmail.toLowerCase().includes(q),
    );
  });

  // ── Modal state ───────────────────────────────────────────────
  readonly activePanel = signal<ActivePanel>(null);
  readonly editingRole = signal<RoleDto | null>(null);

  /** Roles assignable to members (default + custom). */
  readonly assignableRoles = computed(() => this.roleService.roles());

  /** Repo members as manager picker options (for the invite form). */
  readonly memberOptions = computed<UserOption[]>(() =>
    this.membersService.members().map(m => ({ id: m.userId, name: m.userName, email: m.userEmail, avatarClass: m.avatarClass })));

  /** Finds the default permission-role id whose name matches the discipline, falling back to the first available role. @param discipline Discipline value. @returns Role id or ''. */
  private defaultRoleIdFor(discipline: string): string {
    const roles = this.roleService.roles();
    const match = roles.find(r => r.isDefault && r.name === discipline);
    return match?.id ?? roles[0]?.id ?? '';
  }

  /** Updates the add-member discipline and auto-selects its matching default role. @param value Discipline value. */
  onAddDefaultRoleChange(value: string): void {
    this.addDefaultRole.set(value);
    this.addRoleId.set(this.defaultRoleIdFor(value));
  }

  /** Updates the invite-by-email discipline and auto-selects its matching default role. @param value Discipline value. */
  onInviteDefaultRoleChange(value: string): void {
    this.inviteDefaultRole.set(value);
    this.inviteRoleId.set(this.defaultRoleIdFor(value));
  }

  // ── Add member — search state ─────────────────────────────────
  readonly addSearchTerm    = signal('');
  readonly addSearchResults = signal<UserPickerItem[]>([]);
  readonly addSelectedUser  = signal<UserPickerItem | null>(null);
  readonly addDefaultRole   = signal<string>('');
  readonly addRoleId        = signal<string>('');
  readonly addSearching     = signal(false);
  readonly addSubmitting    = signal(false);
  readonly addError         = signal<string | null>(null);
  readonly addDropdownRect  = signal<{ top: number; left: number; width: number } | null>(null);

  private readonly userSearch$ = new Subject<string>();

  // ── Invite form ───────────────────────────────────────────────
  readonly inviteEmail       = signal('');
  readonly inviteDefaultRole = signal<string>('');
  readonly inviteRoleId      = signal<string>('');
  readonly inviteManagerId   = signal<string>('');
  readonly inviteSubmitting  = signal(false);
  readonly inviteError       = signal<string | null>(null);

  // ── Role form ─────────────────────────────────────────────────
  readonly roleFormName        = signal('');
  readonly roleFormDescription = signal('');
  readonly roleFormPermissions = signal<Permission[]>([]);

  constructor() {
    this.userSearch$.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(term => {
        const repoId = this.repoCtx.selectedRepoId();
        if (!repoId || term.length < 2) {
          this.addSearchResults.set([]);
          this.addSearching.set(false);
          return [];
        }
        return this.membersService.searchUsers(repoId, term);
      }),
      takeUntilDestroyed(),
    ).subscribe({
      next:  results => { this.addSearchResults.set(results); this.addSearching.set(false); },
      error: ()      => { this.addSearchResults.set([]); this.addSearching.set(false); },
    });
  }

  /** Opens a panel (modal), pre-populating the role form when editing. @param panel Panel identifier. @param role Optional role to edit. */
  openPanel(panel: ActivePanel, role?: RoleDto): void {
    if (panel === 'add-member'   && !this.privilege.canManageMembers())    return;
    if (panel === 'invite-email' && !this.privilege.canInviteMembers())    return;
    if ((panel === 'create-role' || panel === 'edit-role') && !this.privilege.canManageRoles()) return;
    if (panel === 'add-member') {
      this.addSearchTerm.set('');
      this.addSearchResults.set([]);
      this.addSelectedUser.set(null);
      const firstDiscipline = this.disciplines()[0] ?? '';
      this.addDefaultRole.set(firstDiscipline);
      this.addRoleId.set(this.defaultRoleIdFor(firstDiscipline));
      this.addError.set(null);
      this.addDropdownRect.set(null);
    }
    if (panel === 'invite-email') {
      this.inviteEmail.set('');
      const firstDiscipline = this.disciplines()[0] ?? '';
      this.inviteDefaultRole.set(firstDiscipline);
      this.inviteRoleId.set(this.defaultRoleIdFor(firstDiscipline));
      this.inviteManagerId.set('');
      this.inviteSubmitting.set(false);
      this.inviteError.set(null);
    }
    this.activePanel.set(panel);
    if (panel === 'edit-role' && role) {
      this.editingRole.set(role);
      this.roleFormName.set(role.name);
      this.roleFormDescription.set(role.description ?? '');
      this.roleFormPermissions.set([...role.permissions]);
    } else if (panel === 'create-role') {
      this.editingRole.set(null);
      this.roleFormName.set('');
      this.roleFormDescription.set('');
      this.roleFormPermissions.set([]);
    }
  }

  /** Closes the active panel. */
  closePanel(): void { this.activePanel.set(null); }

  /**
   * Feeds a new search term into the debounced user-search stream and snapshots the input rect for the fixed dropdown.
   * @param term Raw input value.
   * @param el The input element from the DOM event.
   */
  onUserSearch(term: string, el: HTMLInputElement): void {
    this.addSearchTerm.set(term);
    this.addSelectedUser.set(null);
    const trimmed = term.trim();
    if (trimmed.length >= 2) {
      this.addSearching.set(true);
      const r = el.getBoundingClientRect();
      this.addDropdownRect.set({ top: r.bottom + 4, left: r.left, width: r.width });
    } else {
      this.addSearchResults.set([]);
      this.addSearching.set(false);
      this.addDropdownRect.set(null);
    }
    this.userSearch$.next(trimmed);
  }

  /** Selects a user from the search results. @param user Picked user. */
  selectUser(user: UserPickerItem): void {
    this.addSelectedUser.set(user);
    this.addSearchResults.set([]);
    this.addSearchTerm.set('');
    this.addDropdownRect.set(null);
  }

  /** Clears the selected user so a new search can be started. */
  clearSelectedUser(): void {
    this.addSelectedUser.set(null);
    this.addSearchTerm.set('');
    this.addSearchResults.set([]);
    this.addDropdownRect.set(null);
  }

  /** Toggles a permission in the role form. @param p Permission to toggle. */
  togglePermission(p: Permission): void {
    const cur = this.roleFormPermissions();
    this.roleFormPermissions.set(cur.includes(p) ? cur.filter(x => x !== p) : [...cur, p]);
  }

  /** Returns true when a permission is currently selected in the role form. @param p Permission to check. @returns Boolean selection state. */
  hasPermission(p: Permission): boolean {
    return this.roleFormPermissions().includes(p);
  }

  /** Submits the create/edit role form. No-op when the name is blank or no repo is selected. */
  submitRoleForm(): void {
    if (!this.privilege.canManageRoles()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || !this.roleFormName().trim()) return;

    const payload: CreateRolePayload | UpdateRolePayload = {
      name:        this.roleFormName().trim(),
      description: this.roleFormDescription().trim() || null,
      permissions: this.roleFormPermissions(),
    };

    const editing = this.editingRole();
    const obs$: Observable<unknown> = editing
      ? this.roleService.update(repoId, editing.id, payload)
      : this.roleService.create(repoId, payload);

    obs$.subscribe({
      next:  () => this.closePanel(),
      error: (err: HttpErrorResponse) => this.toast.error(this.roleErrorMessage(err)),
    });
  }

  /** Deletes a custom role after confirmation via the shared dialog. @param role Role to delete. */
  async deleteRole(role: RoleDto): Promise<void> {
    if (!this.privilege.canManageRoles()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const ok = await this.confirm.ask({
      titleKey:   'role.deleteTitle',
      subject:    role.name,
      messageKey: 'role.deleteMessage',
      confirmKey: 'role.deleteConfirm',
      type:       'danger',
    });
    if (!ok) return;

    this.roleService.delete(repoId, role.id).subscribe({
      error: (err: HttpErrorResponse) => this.toast.error(this.roleErrorMessage(err)),
    });
  }

  /** Extracts a human-readable message from a role create/update/delete error response. @param err The HTTP error. @returns Message to show in the toast. */
  private roleErrorMessage(err: HttpErrorResponse): string {
    return err.error?.error ?? err.error?.title ?? 'Có lỗi xảy ra. Vui lòng thử lại.';
  }

  /** Clones a role (default or custom) into a new custom role, prompting for the new name. @param role Source role to clone. */
  cloneRole(role: RoleDto): void {
    if (!this.privilege.canManageRoles()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const newName = prompt('Name for the cloned role:', `${role.name} (Copy)`)?.trim();
    if (!newName) return;

    this.roleService.clone(repoId, role.id, newName).subscribe({
      error: (err: HttpErrorResponse) => this.toast.error(this.roleErrorMessage(err)),
    });
  }

  /** Changes a member's discipline, preserving their current assigned role. @param member Target member. @param value New discipline value from select. */
  changeDefaultRole(member: MemberApiDto, value: string): void {
    if (!this.privilege.canManageMembers()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    // Guard: keep at least one Scrum Master discipline.
    if (member.defaultRole === this.SCRUM_MASTER && value !== this.SCRUM_MASTER && this.smDisciplineCount() <= 1) {
      this.toast.warning('At least one member must keep the Scrum Master discipline. Assign it to another member first.');
      this.membersService.load(repoId);
      return;
    }

    this.membersService.updateMemberRole(repoId, member.memberId, value, member.roleId)
      .subscribe({ error: () => this.membersService.load(repoId) });
  }

  /** Assigns a role to a member, preserving their team role (discipline). @param member Target member. @param value Role ID string. */
  assignRole(member: MemberApiDto, value: string): void {
    if (!this.privilege.canManageMembers() || !value) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    // Guard: keep at least one member holding the Scrum Master role.
    const smRoleId = this.scrumMasterRoleId();
    if (member.roleId === smRoleId && value !== smRoleId && this.smRoleCount() <= 1) {
      this.toast.warning('At least one member must keep the Scrum Master role. Assign it to another member first.');
      this.membersService.load(repoId);
      return;
    }

    this.membersService.updateMemberRole(repoId, member.memberId, member.defaultRole, value)
      .subscribe({ error: () => this.membersService.load(repoId) });
  }

  /** Removes a member from the repository after confirmation via the shared dialog. @param member Member to remove. */
  async removeMember(member: MemberApiDto): Promise<void> {
    if (!this.privilege.canManageMembers()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    // Guard: keep at least one Scrum Master discipline and one Scrum Master role.
    if (member.defaultRole === this.SCRUM_MASTER && this.smDisciplineCount() <= 1) {
      this.toast.warning('Cannot remove the last Scrum Master (discipline). Assign the Scrum Master discipline to another member first.');
      return;
    }
    if (member.roleId === this.scrumMasterRoleId() && this.smRoleCount() <= 1) {
      this.toast.warning('Cannot remove the last member holding the Scrum Master role. Assign it to another member first.');
      return;
    }

    const ok = await this.confirm.ask({
      titleKey:   'member.removeTitle',
      subject:    member.userName,
      messageKey: 'member.removeMessage',
      confirmKey: 'member.removeConfirm',
      type:       'danger',
    });
    if (!ok) return;
    this.membersService.removeMember(repoId, member.memberId).subscribe();
  }

  /** Submits the add-member form: calls POST /repositories/{repoId}/members and closes on success. */
  submitAddMember(): void {
    if (!this.privilege.canManageMembers()) return;
    const repoId = this.repoCtx.selectedRepoId();
    const user   = this.addSelectedUser();
    const roleId = this.addRoleId();
    if (!repoId || !user || !roleId) return;
    this.addSubmitting.set(true);
    this.addError.set(null);
    this.membersService.addMember(repoId, user.userId, this.addDefaultRole(), roleId).subscribe({
      next:  () => { this.addSubmitting.set(false); this.closePanel(); },
      error: () => { this.addSubmitting.set(false); this.addError.set('Failed to add member. They may already be in this repository.'); },
    });
  }

  /** Submits the invite-by-email form: calls POST /repositories/{repoId}/invitations and closes on success. */
  submitInvite(): void {
    if (!this.privilege.canInviteMembers()) return;
    const repoId = this.repoCtx.selectedRepoId();
    const email  = this.inviteEmail().trim();
    const roleId = this.inviteRoleId();
    if (!repoId || !email || !roleId) return;

    this.inviteSubmitting.set(true);
    this.inviteError.set(null);
    this.invitationsService.sendInvite(repoId, email, this.inviteDefaultRole(), roleId, this.inviteManagerId() || null).subscribe({
      next:  () => { this.inviteSubmitting.set(false); this.closePanel(); },
      error: (err: HttpErrorResponse) => {
        this.inviteSubmitting.set(false);
        this.inviteError.set(err.error?.error ?? 'Failed to send invite. Please try again.');
      },
    });
  }

  /** Human-readable label for an invitation status. @param status Numeric InvitationStatus. @returns Display label. */
  invitationStatusLabel(status: InvitationStatus): string {
    switch (status) {
      case InvitationStatus.Pending:  return 'Pending';
      case InvitationStatus.Accepted: return 'Accepted';
      case InvitationStatus.Expired:  return 'Expired';
      case InvitationStatus.Revoked:  return 'Revoked';
    }
  }

  /** Badge color classes for an invitation status. @param status Numeric InvitationStatus. @returns Tailwind class string. */
  invitationStatusClass(status: InvitationStatus): string {
    switch (status) {
      case InvitationStatus.Pending:  return 'bg-amber-400/10 text-amber-400 ring-amber-400/30';
      case InvitationStatus.Accepted: return 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30';
      case InvitationStatus.Expired:  return 'bg-slate-700/50 text-slate-400 ring-slate-600/50';
      case InvitationStatus.Revoked:  return 'bg-rose-500/10 text-rose-400 ring-rose-500/30';
    }
  }

  /** Revokes a still-pending invitation after confirmation via the shared dialog. @param invitation Invitation to revoke. */
  async revokeInvitation(invitation: InvitationListItemDto): Promise<void> {
    if (!this.privilege.canInviteMembers()) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    const ok = await this.confirm.ask({
      titleKey:   'invitation.revokeTitle',
      subject:    invitation.email,
      messageKey: 'invitation.revokeMessage',
      confirmKey: 'invitation.revokeConfirm',
      type:       'danger',
    });
    if (!ok) return;

    this.invitationsService.revoke(repoId, invitation.id).subscribe({
      error: (err: HttpErrorResponse) => this.toast.error(err.error?.error ?? 'Failed to revoke invitation. Please try again.'),
    });
  }
}
