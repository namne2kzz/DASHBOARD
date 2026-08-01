import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { SystemUsersService } from '../../services/system-users.service';
import { AuthService } from '../../services/auth.service';
import { DateTimeService } from '../../core/services/date-time.service';
import { SystemUserDto, CreateUserPayload, AuthProvider, UserHierarchy, UserHierarchyNode } from '../../models/system-user.model';
import { PrivilegeService } from '../../core/services/privilege.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { UserSelectComponent, UserOption } from '../../components/user-select/user-select.component';

type UserFilter = 'all' | 'active' | 'inactive' | 'admins';

/** A node in the rendered org-chart tree. */
interface OrgNode {
  id: string;
  name: string;
  email: string;
  avatarClass: string;
  isGlobalAdmin: boolean;
  isActive: boolean;
  subordinateCount: number;
  isSelf: boolean;
  children: OrgNode[];
}

// Tailwind 600-shade colors, validated server-side by `^bg-[a-z]+-\d{3}$`.
const AVATAR_PALETTE = [
  'bg-sky-600', 'bg-violet-600', 'bg-emerald-600', 'bg-amber-600',
  'bg-rose-600', 'bg-indigo-600', 'bg-teal-600', 'bg-fuchsia-600',
  'bg-cyan-600', 'bg-orange-600', 'bg-lime-600', 'bg-pink-600',
];

@Component({
  selector: 'app-settings-users-page',
  standalone: true,
  imports: [FormsModule, NgClass, NgTemplateOutlet, UserSelectComponent],
  templateUrl: './settings-users-page.component.html',
  styleUrls: ['./settings-users-page.component.css'],
})
export class SettingsUsersPageComponent implements OnInit {
  protected readonly svc       = inject(SystemUsersService);
  protected readonly auth      = inject(AuthService);
  protected readonly dt        = inject(DateTimeService);
  protected readonly privilege = inject(PrivilegeService);
  private  readonly toast      = inject(ToastService);
  protected readonly AuthProvider = AuthProvider;

  // ── Filter / search ───────────────────────────────────────────
  readonly searchQuery   = signal('');
  readonly activeFilter  = signal<UserFilter>('all');

  readonly filteredUsers = computed(() => {
    const q      = this.searchQuery().toLowerCase().trim();
    const filter = this.activeFilter();
    return this.svc.users().filter(u => {
      const matchSearch  = !q || u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q);
      const matchFilter  =
        filter === 'active'   ? u.isActive :
        filter === 'inactive' ? !u.isActive :
        filter === 'admins'   ? u.isGlobalAdmin : true;
      return matchSearch && matchFilter;
    });
  });

  readonly filterCounts = computed((): Record<UserFilter, number> => ({
    all:      this.svc.users().length,
    active:   this.svc.users().filter(u =>  u.isActive).length,
    inactive: this.svc.users().filter(u => !u.isActive).length,
    admins:   this.svc.users().filter(u =>  u.isGlobalAdmin).length,
  }));

  protected readonly filterTabs: Array<{ key: UserFilter; label: string }> = [
    { key: 'all',      label: 'All' },
    { key: 'active',   label: 'Active' },
    { key: 'inactive', label: 'Inactive' },
    { key: 'admins',   label: 'Admins' },
  ];

  // ── Expand rows ───────────────────────────────────────────────
  private readonly expandedIds = signal<Set<string>>(new Set());

  /** Returns true when a user row is expanded to show repo memberships. @param userId User ID. @returns Boolean expanded state. */
  isExpanded(userId: string): boolean { return this.expandedIds().has(userId); }

  /** Toggles the repo-membership expansion for a user row. @param userId User ID. */
  toggleExpand(userId: string): void {
    this.expandedIds.update(set => {
      const next = new Set(set);
      next.has(userId) ? next.delete(userId) : next.add(userId);
      return next;
    });
  }

  // ── Role-based guards ─────────────────────────────────────────
  private readonly activeAdminCount = computed(() =>
    this.svc.users().filter(u => u.isGlobalAdmin && u.isActive).length,
  );

  /**
   * Returns false when the target is the currently logged-in user — prevents an admin from
   * demoting or deactivating their own account through this screen (self-lockout).
   * @param user Target user row.
   * @returns True when the current user may promote/demote/activate/deactivate this row.
   */
  canActOnUser(user: SystemUserDto): boolean {
    return user.userId !== this.auth.currentUser()?.userId;
  }

  /** Promotes or demotes the admin flag on a user. @param user Target user. */
  toggleAdmin(user: SystemUserDto): void {
    if (!this.privilege.canManageMembers() || !this.canActOnUser(user)) return;
    if (user.isGlobalAdmin) {
      if (this.activeAdminCount() <= 1) {
        this.toast.warning('Cannot demote the last active global admin account.');
        return;
      }
      this.svc.demoteAdmin(user.userId).subscribe();
    } else {
      if (!user.isActive) {
        this.toast.warning('Cannot promote an inactive account to global admin.');
        return;
      }
      this.svc.promoteAdmin(user.userId).subscribe();
    }
  }

  /** Activates or deactivates a user account. @param user Target user. */
  toggleActive(user: SystemUserDto): void {
    if (!this.privilege.canManageMembers() || !this.canActOnUser(user)) return;
    if (user.isActive && user.isGlobalAdmin && this.activeAdminCount() <= 1) {
      this.toast.warning('Cannot deactivate the last active global admin account.');
      return;
    }
    if (user.isActive) {
      this.svc.deactivate(user.userId).subscribe();
    } else {
      this.svc.activate(user.userId).subscribe();
    }
  }

  // ── Manager (org hierarchy) ───────────────────────────────────

  /**
   * Candidate managers for a user: everyone except the user themselves and their descendants
   * (assigning a descendant as manager would create a cycle — also blocked server-side).
   * @param user The user whose manager is being chosen.
   * @returns Selectable users ordered by name.
   */
  managerCandidates(user: SystemUserDto): SystemUserDto[] {
    const blocked = this.descendantIds(user.userId);
    blocked.add(user.userId);
    return this.svc.users()
      .filter(u => !blocked.has(u.userId))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  /** All users as picker options (for the create-account manager field). */
  readonly allUserOptions = computed<UserOption[]>(() =>
    this.svc.users().map(u => ({ id: u.userId, name: u.name, email: u.email, avatarClass: u.avatarClass })));

  /** Manager options for a specific user (excludes self + descendants). @param user The user whose manager is chosen. */
  candidateOptions(user: SystemUserDto): UserOption[] {
    return this.managerCandidates(user)
      .map(u => ({ id: u.userId, name: u.name, email: u.email, avatarClass: u.avatarClass }));
  }

  /** Collects all descendant user IDs of a user via the manager graph. @param userId Root user ID. */
  private descendantIds(userId: string): Set<string> {
    const result = new Set<string>();
    const queue = [userId];
    const all = this.svc.users();
    while (queue.length) {
      const current = queue.shift()!;
      for (const u of all) {
        if (u.managerId === current && !result.has(u.userId)) {
          result.add(u.userId);
          queue.push(u.userId);
        }
      }
    }
    return result;
  }

  /**
   * Assigns (or clears) a user's manager. Global admins only.
   * @param user Target user.
   * @param managerId Selected manager ID, or empty string to clear.
   */
  changeManager(user: SystemUserDto, managerId: string): void {
    if (!this.privilege.isGlobalAdmin()) return;
    this.svc.setManager(user.userId, managerId || null).subscribe({
      next:  () => this.toast.success('Manager updated'),
      error: err => this.toast.error(err?.error?.error ?? 'Failed to update manager'),
    });
  }

  // ── Hierarchy modal ───────────────────────────────────────────
  readonly hierarchyUser    = signal<SystemUserDto | null>(null);
  readonly hierarchy        = signal<UserHierarchy | null>(null);
  readonly hierarchyLoading = signal(false);

  /** Opens the org-hierarchy modal for a user and loads the slice. @param user The focus user. */
  openHierarchy(user: SystemUserDto): void {
    this.hierarchyUser.set(user);
    this.hierarchy.set(null);
    this.hierarchyLoading.set(true);
    this.svc.getHierarchy(user.userId).subscribe({
      next:  h  => { this.hierarchy.set(h); this.hierarchyLoading.set(false); },
      error: () => { this.hierarchyLoading.set(false); this.toast.error('Failed to load hierarchy'); },
    });
  }

  /** Closes the hierarchy modal. */
  closeHierarchy(): void {
    this.hierarchyUser.set(null);
    this.hierarchy.set(null);
  }

  /**
   * The org-chart as a nested tree, centred on the focus user: the ancestor spine branches at the
   * direct manager into (peers + self); self branches down into its direct reports.
   */
  readonly orgTree = computed<OrgNode[]>(() => {
    const h = this.hierarchy();
    if (!h) return [];

    const toNode = (n: UserHierarchyNode, isSelf = false, children: OrgNode[] = []): OrgNode => ({
      id: n.id, name: n.name, email: n.email, avatarClass: n.avatarClass,
      isGlobalAdmin: n.isGlobalAdmin, isActive: n.isActive, subordinateCount: n.subordinateCount,
      isSelf, children,
    });

    const selfNode = toNode(h.self, true, h.subordinates.map(s => toNode(s)));
    const siblings = [...h.peers.map(p => toNode(p)), selfNode]
      .sort((a, b) => a.name.localeCompare(b.name));

    // No ancestors → the focus user is a root; render the same-level row as a forest.
    if (h.ancestors.length === 0) return siblings;

    // Build the manager spine (root → … → direct manager), then hang the sibling row off the manager.
    const chain = h.ancestors.map(a => toNode(a));
    for (let i = 0; i < chain.length - 1; i++) chain[i].children = [chain[i + 1]];
    chain[chain.length - 1].children = siblings;
    return [chain[0]];
  });

  /** Re-centres the tree on a clicked node (no-op for the current user). @param node The clicked org node. */
  recenter(node: OrgNode): void {
    if (node.isSelf) return;
    const u = this.svc.users().find(x => x.userId === node.id);
    if (u) this.openHierarchy(u);
  }

  // ── Create account modal ──────────────────────────────────────
  readonly showCreateModal = signal(false);
  readonly formName        = signal('');
  readonly formEmail       = signal('');
  readonly formPassword    = signal('');
  readonly formConfirm     = signal('');
  readonly formIsAdmin     = signal(false);
  readonly formManagerId   = signal('');
  readonly formError       = signal<string | null>(null);
  readonly showPassword    = signal(false);
  readonly showConfirm     = signal(false);

  readonly passwordMismatch = computed(() =>
    !!this.formConfirm() && this.formPassword() !== this.formConfirm(),
  );

  readonly formValid = computed(() =>
    !!this.formName().trim() &&
    !!this.formEmail().trim() &&
    this.formPassword().length >= 8 &&
    !this.passwordMismatch(),
  );

  /** Opens the create-account modal and resets the form. */
  openCreateModal(): void {
    if (!this.privilege.isGlobalAdmin()) return;
    this.formName.set('');
    this.formEmail.set('');
    this.formPassword.set('');
    this.formConfirm.set('');
    this.formIsAdmin.set(false);
    this.formManagerId.set('');
    this.formError.set(null);
    this.showPassword.set(false);
    this.showConfirm.set(false);
    this.showCreateModal.set(true);
  }

  /** Closes the create-account modal. */
  closeCreateModal(): void { this.showCreateModal.set(false); }

  /** Submits the create-account form. No-op when the form is invalid. */
  submitCreate(): void {
    if (!this.privilege.isGlobalAdmin()) return;
    if (!this.formValid()) return;
    this.formError.set(null);

    const payload: CreateUserPayload = {
      name:          this.formName().trim(),
      email:         this.formEmail().trim(),
      password:      this.formPassword(),
      isGlobalAdmin: this.formIsAdmin(),
      avatarClass:   AVATAR_PALETTE[Math.floor(Math.random() * AVATAR_PALETTE.length)],
      managerId:     this.formManagerId() || null,
    };

    this.svc.create(payload).subscribe({
      next:  () => this.closeCreateModal(),
      error: () => this.formError.set('Failed to create account. Email may already be in use.'),
    });
  }

  ngOnInit(): void { this.svc.load(); }
}
