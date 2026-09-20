import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StorageKeys } from '../constants/storage-keys.constant';
import { DATE_FORMAT_PATTERN, SETTING_KEYS, TIMEZONE_OFFSET } from '../constants/system.constant';
import type { AppLanguage, DateFormat, TimezoneId } from '../../models/preferences.model';

export type { AppLanguage, DateFormat, TimezoneId };

/**
 * Persists all per-user display and notification preferences.
 * Signals are seeded from localStorage immediately so the UI renders without delay.
 * After login the service fetches stored settings from the API and overrides the signals.
 * Each mutation updates localStorage synchronously and calls the API in the background.
 */
@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private readonly http   = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/users/me/settings`;

  // ── UI display ──────────────────────────────────────────────────────────────
  readonly dateFormat = signal<DateFormat>(
    (localStorage.getItem(StorageKeys.dateFormat) as DateFormat | null) ?? 'dmy',
  );
  readonly timezone = signal<TimezoneId>(
    (localStorage.getItem(StorageKeys.timezone) as TimezoneId | null) ?? '',
  );
  readonly language = signal<AppLanguage>(
    (localStorage.getItem('nxs.ui.language') as AppLanguage | null) ?? 'en',
  );

  // ── Notifications ───────────────────────────────────────────────────────────
  readonly notifyEmail  = signal<boolean>(this._loadBool('nxs.notify.email',         true));
  readonly notifyPush   = signal<boolean>(this._loadBool('nxs.notify.push',          false));
  readonly notifyDigest = signal<boolean>(this._loadBool('nxs.notify.digest',        false));
  readonly mentionsOnly = signal<boolean>(this._loadBool('nxs.notify.mentions-only', false));

  // ── Accessibility / UX ──────────────────────────────────────────────────────
  readonly reduceMotion = signal<boolean>(this._loadBool('nxs.a11y.reduce-motion', false));
  readonly compactMode  = signal<boolean>(this._loadBool('nxs.ui.compact-mode',    false));

  constructor() {
    // Mirror all changes to localStorage immediately.
    effect(() => localStorage.setItem(StorageKeys.dateFormat,       this.dateFormat()));
    effect(() => localStorage.setItem(StorageKeys.timezone,         this.timezone()));
    effect(() => localStorage.setItem('nxs.ui.language',            this.language()));
    effect(() => localStorage.setItem('nxs.notify.email',           String(this.notifyEmail())));
    effect(() => localStorage.setItem('nxs.notify.push',            String(this.notifyPush())));
    effect(() => localStorage.setItem('nxs.notify.digest',          String(this.notifyDigest())));
    effect(() => localStorage.setItem('nxs.notify.mentions-only',   String(this.mentionsOnly())));
    effect(() => localStorage.setItem('nxs.a11y.reduce-motion',     String(this.reduceMotion())));
    effect(() => localStorage.setItem('nxs.ui.compact-mode',        String(this.compactMode())));
  }

  // ── Server sync ─────────────────────────────────────────────────────────────

  /**
   * Pulls all persisted settings from the backend and applies them to signals.
   * Call once after login / token refresh. Errors are swallowed — localStorage is fallback.
   *
   * Returns an Observable that emits the stored avatar URL (or null) so the caller
   * can call `auth.patchProfile({ avatarUrl })` without creating a circular dependency.
   * @returns Observable emitting the avatar URL string, or null if not set.
   */
  syncFromServer(): Observable<string | null> {
    return this.http.get<Record<string, string | null>>(this.apiUrl).pipe(
      catchError(() => of({} as Record<string, string | null>)),
      map(s => {
        const v = (k: string) => s[k];
        if (v(SETTING_KEYS.dateFormat))   this.dateFormat.set(v(SETTING_KEYS.dateFormat) as DateFormat);
        const tz = v(SETTING_KEYS.timezone);
        if (tz !== undefined && tz !== null) this.timezone.set(tz as TimezoneId);
        if (v(SETTING_KEYS.language))     this.language.set(v(SETTING_KEYS.language) as AppLanguage);
        if (v(SETTING_KEYS.notifyEmail)  != null) this.notifyEmail.set(v(SETTING_KEYS.notifyEmail) === 'true');
        if (v(SETTING_KEYS.notifyPush)   != null) this.notifyPush.set(v(SETTING_KEYS.notifyPush) === 'true');
        if (v(SETTING_KEYS.notifyDigest) != null) this.notifyDigest.set(v(SETTING_KEYS.notifyDigest) === 'true');
        if (v(SETTING_KEYS.mentionsOnly) != null) this.mentionsOnly.set(v(SETTING_KEYS.mentionsOnly) === 'true');
        if (v(SETTING_KEYS.reduceMotion) != null) this.reduceMotion.set(v(SETTING_KEYS.reduceMotion) === 'true');
        if (v(SETTING_KEYS.compactMode)  != null) this.compactMode.set(v(SETTING_KEYS.compactMode) === 'true');

        // Return avatarUrl so the caller can patch the auth profile (avoids circular DI).
        return v(SETTING_KEYS.avatarUrl) ?? null;
      }),
    );
  }

  // ── Setters (each updates signal + persists to server) ─────────────────────

  /** @param value Date format preference. */
  setDateFormat(value: DateFormat): void   { this.dateFormat.set(value);  this._save({ [SETTING_KEYS.dateFormat]: value }); }
  /** @param value Timezone id ('' = browser local). */
  setTimezone(value: TimezoneId): void     { this.timezone.set(value);    this._save({ [SETTING_KEYS.timezone]: value }); }
  /** @param value Interface language code. */
  setLanguage(value: AppLanguage): void    { this.language.set(value);    this._save({ [SETTING_KEYS.language]: value }); }
  /** @param value Whether to receive email notifications. */
  setNotifyEmail(value: boolean): void     { this.notifyEmail.set(value); this._save({ [SETTING_KEYS.notifyEmail]: String(value) }); }
  /** @param value Whether to receive push notifications. */
  setNotifyPush(value: boolean): void      { this.notifyPush.set(value);  this._save({ [SETTING_KEYS.notifyPush]: String(value) }); }
  /** @param value Whether to receive a digest email of unread activity. */
  setNotifyDigest(value: boolean): void    { this.notifyDigest.set(value); this._save({ [SETTING_KEYS.notifyDigest]: String(value) }); }
  /** @param value Whether to receive notifications only for mentions. */
  setMentionsOnly(value: boolean): void    { this.mentionsOnly.set(value); this._save({ [SETTING_KEYS.mentionsOnly]: String(value) }); }
  /** @param value Whether to reduce UI animations. */
  setReduceMotion(value: boolean): void    { this.reduceMotion.set(value); this._save({ [SETTING_KEYS.reduceMotion]: String(value) }); }
  /** @param value Whether to use compact row density. */
  setCompactMode(value: boolean): void     { this.compactMode.set(value);  this._save({ [SETTING_KEYS.compactMode]: String(value) }); }

  // ── Derived helpers ─────────────────────────────────────────────────────────

  /** The Angular date-part pattern for the current date-format preference. */
  datePattern(): string { return DATE_FORMAT_PATTERN[this.dateFormat()]; }

  /** The Angular `formatDate` timezone offset for the current preference (undefined = local). */
  timezoneOffset(): string | undefined { return TIMEZONE_OFFSET[this.timezone()]; }

  // ── Internals ───────────────────────────────────────────────────────────────

  /** Fire-and-forget PUT to the settings endpoint; ignores failures (localStorage is source of truth during session). */
  private _save(patch: Record<string, string | null>): void {
    this.http.put(this.apiUrl, { settings: patch })
      .pipe(catchError(() => of(null)))
      .subscribe();
  }

  /** Reads a boolean from localStorage; returns {@param defaultValue} if missing. */
  private _loadBool(key: string, defaultValue: boolean): boolean {
    const v = localStorage.getItem(key);
    return v === null ? defaultValue : v === 'true';
  }
}
