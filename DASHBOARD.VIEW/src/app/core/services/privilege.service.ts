import { computed, inject, Injectable } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { MembersService } from '../../services/members.service';
import { RoleService } from '../../services/role.service';
import { Permission } from '../enums/system.enum';

@Injectable({ providedIn: 'root' })
export class PrivilegeService {
  private readonly auth        = inject(AuthService);
  private readonly members     = inject(MembersService);
  private readonly roleService = inject(RoleService);

  /** The current user's member record in the active repository. */
  private readonly currentMember = computed(() => {
    const userId = this.auth.currentUser()?.userId;
    return userId ? this.members.getMember(userId) : null;
  });

  /**
   * The current member's effective permissions — exactly the permissions of their assigned role.
   * The team role (discipline) grants no permissions. Empty when not a member.
   */
  private readonly rolePermissions = computed<ReadonlySet<Permission>>(() => {
    const id = this.currentMember()?.roleId;
    if (!id) return new Set<Permission>();
    const role = this.roleService.roles().find(r => r.id === id);
    return new Set<Permission>(role?.permissions ?? []);
  });

  /** True when the current user is a global admin or their assigned role grants the given permission. @param p Permission to check. */
  private can(p: Permission): boolean {
    return this.isGlobalAdmin() || this.rolePermissions().has(p);
  }

  /** True when the logged-in user carries the global-admin flag. */
  readonly isGlobalAdmin = computed(() => this.auth.currentUser()?.isGlobalAdmin ?? false);

  /** The current user's team role (discipline) value in the active repository; null when not a member. */
  readonly currentRole = computed<string | null>(() => this.currentMember()?.defaultRole ?? null);

  /** Can view and manage repository members and pending invitations. */
  readonly canManageMembers = computed(() => this.can(Permission.ManageMembers));

  /** Can create, activate, and close sprints. */
  readonly canManageSprints = computed(() => this.can(Permission.ManageSprint));

  /** Can activate a sprint (transition it to Active state). */
  readonly canActivateSprint = computed(() => this.can(Permission.ActivateSprint));

  /** Can promote a ready backlog item into an active sprint. */
  readonly canPromoteToSprint = computed(() => this.can(Permission.PromoteToSprint));

  /** Can delete any work item in the repository. */
  readonly canDeleteWorkItem = computed(() => this.can(Permission.DeleteWorkItem));

  /** Can assign a sprint task to a repository member. */
  readonly canAssignWorkItem = computed(() => this.can(Permission.AssignWorkItem));

  /** Can create and modify custom roles for the repository. */
  readonly canManageRoles = computed(() => this.can(Permission.ManageRoles));

  /** Can invite new members into the repository. */
  readonly canInviteMembers = computed(() => this.can(Permission.InviteMembers));

  /** Can manage repository metadata and archive settings. */
  readonly canManageRepository = computed(() => this.can(Permission.EditRepository));

  /** Can manage repository-level metadata fields. */
  readonly canManageMetadata = computed(() => this.can(Permission.ManageMetadata));

  /** Can configure board columns, WIP limits, split settings, and move cards. */
  readonly canManageBoard = computed(() => this.can(Permission.ManageBoard));

  /** Can create new work items in the repository. */
  readonly canCreateWorkItem = computed(() => this.can(Permission.CreateWorkItem));

  /** Can edit work items and change their state. */
  readonly canEditWorkItem = computed(() => this.can(Permission.EditWorkItem));

  /** Can rank and manage the product backlog. */
  readonly canManageBacklog = computed(() => this.can(Permission.ManageBacklog));

  /** Can manage sprint capacity configuration and days off. */
  readonly canManageCapacity = computed(() => this.can(Permission.ManageCapacity));

  /** Can view analytics and reporting dashboards. */
  readonly canViewAnalytics = computed(() => this.can(Permission.ViewAnalytics));

  /** Can manage pipeline configurations and runs. */
  readonly canManagePipeline = computed(() => this.can(Permission.ManagePipeline));

  /**
   * Whether the current user may perform a management action on a member (including themselves).
   * Permission-based: GlobalAdmin or a member holding ManageMembers.
   * Server-side guards enforce last-admin protection, so self-edit cannot strand the repo.
   * @param _targetUserId ID of the member being targeted (unused — any member is actionable).
   * @returns True when the action is permitted.
   */
  canActOnMember(_targetUserId: string): boolean {
    return this.can(Permission.ManageMembers);
  }
}
