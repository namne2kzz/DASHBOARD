import { NgClass } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { DateTimeService } from '../../core/services/date-time.service';
import { PreferencesService } from '../../core/services/preferences.service';
import type { DateFormat, TimezoneId } from '../../models/preferences.model';
import { ThemeService } from '../../core/services/theme.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { AuthService } from '../../services/auth.service';
import { MembersService } from '../../services/members.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, FormsModule, NgClass, LocalDatePipe],
  templateUrl: './profile-page.component.html',
  styleUrl: './profile-page.component.scss',
})
export class ProfilePageComponent {
  private readonly auth    = inject(AuthService);
  private readonly fb      = inject(FormBuilder);
  private readonly http    = inject(HttpClient);
  private readonly toast   = inject(ToastService);
  readonly members  = inject(MembersService);
  readonly repoCtx  = inject(RepositoryContextService);
  readonly dt       = inject(DateTimeService);
  readonly prefs    = inject(PreferencesService);
  readonly themeSvc = inject(ThemeService);

  readonly user = computed(() => this.auth.currentUser());

  readonly currentMember = computed(() => {
    const uid = this.user()?.userId;
    return uid ? this.members.getMember(uid) : null;
  });

  readonly daysInRepo = computed(() => {
    const joinedAt = this.currentMember()?.joinedAt;
    const date = this.dt.toLocal(joinedAt);
    if (!date) return null;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    date.setHours(0, 0, 0, 0);
    return Math.floor((today.getTime() - date.getTime()) / 86_400_000);
  });

  readonly role = computed(() => {
    const m = this.currentMember();
    return m ? this.members.roleName(m) : null;
  });

  readonly profileForm = this.fb.group({
    name: [this.auth.currentUser()?.name ?? '', [Validators.required, Validators.minLength(2)]],
  });

  readonly passwordForm = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword:     ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  readonly savingProfile  = signal(false);
  readonly savingPassword = signal(false);
  readonly showCurrent    = signal(false);
  readonly showNew        = signal(false);
  readonly showConfirm    = signal(false);

  // ── Notification preferences (UI mockup — not yet persisted) ─────────────────
  readonly notifyAssigned    = signal(true);
  readonly notifyMentioned   = signal(true);
  readonly notifyStateChange = signal(false);
  readonly notifyDigest      = signal(false);

  // ── Preferences ──────────────────────────────────────────────────────────────
  readonly DATE_FORMAT_OPTIONS: ReadonlyArray<{ value: DateFormat; label: string; example: string }> = [
    { value: 'dmy', label: 'DD/MM/YYYY', example: '25/12/2026' },
    { value: 'mdy', label: 'MM/DD/YYYY', example: '12/25/2026' },
    { value: 'ymd', label: 'YYYY-MM-DD', example: '2026-12-25' },
  ];

  readonly TIMEZONE_OPTIONS: ReadonlyArray<{ value: TimezoneId; label: string }> = [
    { value: '',      label: 'Browser local time' },
    { value: 'utc0',  label: 'UTC+0 (London)' },
    { value: 'utc7',  label: 'UTC+7 (Bangkok / Hanoi)' },
    { value: 'utc9',  label: 'UTC+9 (Tokyo / Seoul)' },
    { value: 'utc-5', label: 'UTC−5 (New York)' },
  ];

  /** Saves the display name to the backend and updates the local auth signal. */
  saveProfile(): void {
    if (this.profileForm.invalid || this.savingProfile()) return;
    const uid = this.user()?.userId;
    if (!uid) return;
    this.savingProfile.set(true);
    const name = this.profileForm.value.name!.trim();
    this.http.put(`${environment.apiBaseUrl}/users/${uid}/profile`, { name }).subscribe({
      next: () => {
        this.auth.currentUser.update(u => (u ? { ...u, name } : u));
        this.profileForm.markAsPristine();
        this.toast.success('Profile updated');
        this.savingProfile.set(false);
      },
      error: () => {
        this.toast.error('Failed to update profile');
        this.savingProfile.set(false);
      },
    });
  }

  /** Sends a change-password request, then resets the password form on success. */
  changePassword(): void {
    if (this.passwordForm.invalid || this.savingPassword()) return;
    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;
    if (newPassword !== confirmPassword) {
      this.toast.error('New passwords do not match');
      return;
    }
    const uid = this.user()?.userId;
    if (!uid) return;
    this.savingPassword.set(true);
    this.http
      .put(`${environment.apiBaseUrl}/users/${uid}/password`, { oldPassword: currentPassword, newPassword })
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.toast.success('Password changed');
          this.savingPassword.set(false);
        },
        error: () => {
          this.toast.error('Failed to change password');
          this.savingPassword.set(false);
        },
      });
  }
}
