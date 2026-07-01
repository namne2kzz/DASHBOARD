import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { SystemUsersService } from '../../services/system-users.service';
import { AuthService } from '../../services/auth.service';
import { DateTimeService } from '../../core/services/date-time.service';
import { SystemUserDto, CreateUserPayload } from '../../models/system-user.model';
import { PrivilegeService } from '../../core/services/privilege.service';
import { ToastService } from '../../core/components/toast/toast.service';

type UserFilter = 'all' | 'active' | 'inactive' | 'admins';

// Tailwind 600-shade colors, validated server-side by `^bg-[a-z]+-\d{3}$`.
const AVATAR_PALETTE = [
  'bg-sky-600', 'bg-violet-600', 'bg-emerald-600', 'bg-amber-600',
  'bg-rose-600', 'bg-indigo-600', 'bg-teal-600', 'bg-fuchsia-600',
  'bg-cyan-600', 'bg-orange-600', 'bg-lime-600', 'bg-pink-600',
];

@Component({
  selector: 'app-settings-users-page',
  standalone: true,
  imports: [FormsModule, NgClass],
  templateUrl: './settings-users-page.component.html',
  styleUrls: ['./settings-users-page.component.css'],
})
export class SettingsUsersPageComponent implements OnInit {
  protected readonly svc       = inject(SystemUsersService);
  protected readonly auth      = inject(AuthService);
  protected readonly dt        = inject(DateTimeService);
  protected readonly privilege = inject(PrivilegeService);
  private  readonly toast      = inject(ToastService);

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

  /** Promotes or demotes the admin flag on a user. @param user Target user. */
  toggleAdmin(user: SystemUserDto): void {
    if (!this.privilege.canManageMembers()) return;
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
    if (!this.privilege.canManageMembers()) return;
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

  // ── Create account modal ──────────────────────────────────────
  readonly showCreateModal = signal(false);
  readonly formName        = signal('');
  readonly formEmail       = signal('');
  readonly formPassword    = signal('');
  readonly formConfirm     = signal('');
  readonly formIsAdmin     = signal(false);
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
    };

    this.svc.create(payload).subscribe({
      next:  () => this.closeCreateModal(),
      error: () => this.formError.set('Failed to create account. Email may already be in use.'),
    });
  }

  ngOnInit(): void { this.svc.load(); }
}
