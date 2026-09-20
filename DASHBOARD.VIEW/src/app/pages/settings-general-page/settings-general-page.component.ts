import {
  Component, computed, inject, signal, OnInit, DestroyRef,
} from '@angular/core';
import { NgClass } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ThemeService } from '../../core/services/theme.service';
import { PreferencesService } from '../../core/services/preferences.service';
import type { AppLanguage, DateFormat, TimezoneId } from '../../models/preferences.model';
import { ToastService } from '../../core/components/toast/toast.service';
import { AuthService } from '../../services/auth.service';
import { AvatarService } from '../../services/avatar.service';
import { environment } from '../../../environments/environment';
import { AVATAR_COLORS } from '../../core/constants/system.constant';

@Component({
  selector: 'app-settings-general-page',
  standalone: true,
  imports: [NgClass, FormsModule, ReactiveFormsModule],
  templateUrl: './settings-general-page.component.html',
  styleUrls: ['./settings-general-page.component.css'],
})
export class SettingsGeneralPageComponent implements OnInit {
  readonly themeService    = inject(ThemeService);
  readonly prefs           = inject(PreferencesService);
  readonly avatarService   = inject(AvatarService);
  private readonly auth    = inject(AuthService);
  private readonly fb      = inject(FormBuilder);
  private readonly http    = inject(HttpClient);
  private readonly toast   = inject(ToastService);
  private readonly destroy = inject(DestroyRef);

  readonly user         = computed(() => this.auth.currentUser());
  readonly avatarColors = AVATAR_COLORS;

  // ── Avatar ───────────────────────────────────────────────────────────────────
  /** Selected avatar CSS colour class (persisted to profile via Save profile). */
  readonly selectedColor    = signal<string>(this.auth.currentUser()?.avatarClass ?? 'bg-sky-600');
  /** Signal mirror of profileForm dirty state so computed() can track it. */
  readonly profileFormDirty = signal(false);

  /** Upload state from AvatarService — drives progress bar and error display. */
  readonly uploadState = this.avatarService.uploadState;

  /** True when the name field is dirty OR the colour has changed from the saved value. */
  readonly profileChanged = computed(() =>
    this.profileFormDirty() ||
    this.selectedColor() !== (this.auth.currentUser()?.avatarClass ?? 'bg-sky-600'),
  );

  /** Returns initials (up to 2 chars) for the avatar colour chip fallback. */
  readonly initials = computed(() => {
    const name = this.user()?.name ?? '';
    return name.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase();
  });

  ngOnInit(): void {
    this.profileForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => this.profileFormDirty.set(!this.profileForm.pristine));
  }

  /**
   * Handles file selection: immediately starts the upload flow via AvatarService.
   * No local FileReader preview needed — the uploaded URL will appear once confirmed.
   * @param event The file input change event.
   */
  onAvatarFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (!file) return;
    // Reset file input so the same file can be re-selected if needed.
    input.value = '';
    this.avatarService.uploadAvatar(file);
  }

  // ── Account ──────────────────────────────────────────────────────────────────
  readonly profileForm = this.fb.group({
    name: [this.auth.currentUser()?.name ?? '', [Validators.required, Validators.minLength(2)]],
  });

  readonly savingProfile = signal(false);

  /**
   * Saves the display name and selected avatar colour to the backend, then
   * updates the local auth signal so the header/avatar re-renders immediately.
   */
  saveProfile(): void {
    if (this.profileForm.invalid || this.savingProfile()) return;
    const uid = this.user()?.userId;
    if (!uid) return;

    this.savingProfile.set(true);
    const name        = this.profileForm.value.name!.trim();
    const avatarClass = this.selectedColor();

    this.http
      .put(`${environment.apiBaseUrl}/users/${uid}/profile`, { name, avatarClass })
      .subscribe({
        next: () => {
          this.auth.patchProfile({ name, avatarClass });
          this.profileForm.markAsPristine();
          this.profileFormDirty.set(false);
          this.toast.success('Profile updated');
          this.savingProfile.set(false);
        },
        error: () => {
          this.toast.error('Failed to update profile');
          this.savingProfile.set(false);
        },
      });
  }

  // ── Password ─────────────────────────────────────────────────────────────────
  readonly passwordForm = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword:     ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  readonly savingPassword = signal(false);
  readonly showCurrent    = signal(false);
  readonly showNew        = signal(false);
  readonly showConfirm    = signal(false);

  /** @internal */
  toggleShowCurrent(): void { this.showCurrent.update(v => !v); }
  /** @internal */
  toggleShowNew(): void     { this.showNew.update(v => !v); }
  /** @internal */
  toggleShowConfirm(): void { this.showConfirm.update(v => !v); }

  /** Validates and submits a password change request. */
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

  // ── Preference setters (delegate to PreferencesService) ──────────────────────
  /** @param v Date format value from the select. */
  onDateFormat(v: string): void    { this.prefs.setDateFormat(v as DateFormat); }
  /** @param v Timezone id from the select. */
  onTimezone(v: string): void      { this.prefs.setTimezone(v as TimezoneId); }
  /** @param v Language code from the select. */
  onLanguage(v: string): void      { this.prefs.setLanguage(v as AppLanguage); }
  /** Toggles email notification preference and persists. */
  toggleNotifyEmail(): void        { this.prefs.setNotifyEmail(!this.prefs.notifyEmail()); }
  /** Toggles push notification preference and persists. */
  toggleNotifyPush(): void         { this.prefs.setNotifyPush(!this.prefs.notifyPush()); }
  /** Toggles digest email preference and persists. */
  toggleNotifyDigest(): void       { this.prefs.setNotifyDigest(!this.prefs.notifyDigest()); }
  /** Toggles mentions-only filter and persists. */
  toggleMentionsOnly(): void       { this.prefs.setMentionsOnly(!this.prefs.mentionsOnly()); }
  /** Toggles reduce-motion accessibility setting and persists. */
  toggleReduceMotion(): void       { this.prefs.setReduceMotion(!this.prefs.reduceMotion()); }
  /** Toggles compact density mode and persists. */
  toggleCompactMode(): void        { this.prefs.setCompactMode(!this.prefs.compactMode()); }
}
