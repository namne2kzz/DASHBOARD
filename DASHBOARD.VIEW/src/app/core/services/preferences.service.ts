import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';
import type { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StorageKeys } from '../constants/storage-keys.constant';
import { DATE_FORMAT_PATTERN, LEGACY_TIMEZONE_IDS, SETTING_KEYS } from '../constants/system.constant';
import { browserTimezone, offsetFor } from '../utils/timezone.util';
import type { AppLanguage, DateFormat, TimezoneId } from '../../models/preferences.model';
import { readStorage, writeStorage } from '../utils/safe-storage.util';

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
    (readStorage(StorageKeys.dateFormat) as DateFormat | null) ?? 'dmy',
  );
  /**
   * Preferred display zone as an IANA id; empty means "follow the browser".
   * Seeded from localStorage, migrating any legacy fixed-offset id left from an older build.
   */
  readonly timezone = signal<TimezoneId>(
    (() => {
      const stored = readStorage(StorageKeys.timezone) ?? '';
      return LEGACY_TIMEZONE_IDS[stored] ?? stored;
    })(),
  );
  readonly language = signal<AppLanguage>(
    (readStorage('nxs.ui.language') as AppLanguage | null) ?? 'en',
  );

  // ── Notifications ───────────────────────────────────────────────────────────
  readonly notifyEmail  = signal<boolean>(this._loadBool('nxs.notify.email',         true));
  readonly notifyPush   = signal<boolean>(this._loadBool('nxs.notify.push',          false));
  readonly notifyDigest = signal<boolean>(this._loadBool('nxs.notify.digest',        false));
  readonly notifyMentions = signal<boolean>(this._loadBool('nxs.notify.mentions', true));
  // Per-category in-app toggles (gated under notifyPush). Default on.
  readonly notifyTypeWorkItems  = signal<boolean>(this._loadBool('nxs.notify.type.workitems',  true));
  readonly notifyTypeSprints    = signal<boolean>(this._loadBool('nxs.notify.type.sprints',    true));
  readonly notifyTypeMembership = signal<boolean>(this._loadBool('nxs.notify.type.membership', true));

  // ── Accessibility / UX ──────────────────────────────────────────────────────
  readonly reduceMotion = signal<boolean>(this._loadBool('nxs.a11y.reduce-motion', false));
  readonly compactMode  = signal<boolean>(this._loadBool('nxs.ui.compact-mode',    false));
  /** Whether the floating NMate assistant is shown. Defaults to on so new users discover it. */
  readonly showNMate    = signal<boolean>(this._loadBool('nxs.ui.nmate-widget',    true));

  constructor() {
    // Mirror all changes to localStorage immediately.
    effect(() => writeStorage(StorageKeys.dateFormat,       this.dateFormat()));
    effect(() => writeStorage(StorageKeys.timezone,         this.timezone()));
    effect(() => writeStorage('nxs.ui.language',            this.language()));
    effect(() => writeStorage('nxs.notify.email',           String(this.notifyEmail())));
    effect(() => writeStorage('nxs.notify.push',            String(this.notifyPush())));
    effect(() => writeStorage('nxs.notify.digest',          String(this.notifyDigest())));
    effect(() => writeStorage('nxs.notify.mentions',        String(this.notifyMentions())));
    effect(() => writeStorage('nxs.notify.type.workitems',  String(this.notifyTypeWorkItems())));
    effect(() => writeStorage('nxs.notify.type.sprints',    String(this.notifyTypeSprints())));
    effect(() => writeStorage('nxs.notify.type.membership', String(this.notifyTypeMembership())));
    effect(() => writeStorage('nxs.a11y.reduce-motion',     String(this.reduceMotion())));
    effect(() => writeStorage('nxs.ui.compact-mode',        String(this.compactMode())));
    effect(() => writeStorage('nxs.ui.nmate-widget',        String(this.showNMate())));
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
        if (tz !== undefined && tz !== null) this.setTimezoneFromServer(tz);
        if (v(SETTING_KEYS.language))     this.language.set(v(SETTING_KEYS.language) as AppLanguage);
        if (v(SETTING_KEYS.notifyEmail)  != null) this.notifyEmail.set(v(SETTING_KEYS.notifyEmail) === 'true');
        if (v(SETTING_KEYS.notifyPush)   != null) this.notifyPush.set(v(SETTING_KEYS.notifyPush) === 'true');
        if (v(SETTING_KEYS.notifyDigest) != null) this.notifyDigest.set(v(SETTING_KEYS.notifyDigest) === 'true');
        if (v(SETTING_KEYS.notifyMentions) != null) this.notifyMentions.set(v(SETTING_KEYS.notifyMentions) === 'true');
        if (v(SETTING_KEYS.notifyTypeWorkItems)  != null) this.notifyTypeWorkItems.set(v(SETTING_KEYS.notifyTypeWorkItems) === 'true');
        if (v(SETTING_KEYS.notifyTypeSprints)    != null) this.notifyTypeSprints.set(v(SETTING_KEYS.notifyTypeSprints) === 'true');
        if (v(SETTING_KEYS.notifyTypeMembership) != null) this.notifyTypeMembership.set(v(SETTING_KEYS.notifyTypeMembership) === 'true');
        if (v(SETTING_KEYS.reduceMotion) != null) this.reduceMotion.set(v(SETTING_KEYS.reduceMotion) === 'true');
        if (v(SETTING_KEYS.compactMode)  != null) this.compactMode.set(v(SETTING_KEYS.compactMode) === 'true');
        if (v(SETTING_KEYS.showNMate)    != null) this.showNMate.set(v(SETTING_KEYS.showNMate) === 'true');

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
  /** @param value Whether to notify the user when they are @mentioned in a discussion. */
  setNotifyMentions(value: boolean): void  { this.notifyMentions.set(value); this._save({ [SETTING_KEYS.notifyMentions]: String(value) }); }
  /** @param value Whether to receive work-item notifications (assigned/unassigned/state). */
  setNotifyTypeWorkItems(value: boolean): void  { this.notifyTypeWorkItems.set(value);  this._save({ [SETTING_KEYS.notifyTypeWorkItems]: String(value) }); }
  /** @param value Whether to receive sprint lifecycle notifications. */
  setNotifyTypeSprints(value: boolean): void    { this.notifyTypeSprints.set(value);    this._save({ [SETTING_KEYS.notifyTypeSprints]: String(value) }); }
  /** @param value Whether to receive membership change notifications. */
  setNotifyTypeMembership(value: boolean): void { this.notifyTypeMembership.set(value); this._save({ [SETTING_KEYS.notifyTypeMembership]: String(value) }); }
  /** @param value Whether to reduce UI animations. */
  setReduceMotion(value: boolean): void    { this.reduceMotion.set(value); this._save({ [SETTING_KEYS.reduceMotion]: String(value) }); }
  /** @param value Whether to use compact row density. */
  setCompactMode(value: boolean): void     { this.compactMode.set(value);  this._save({ [SETTING_KEYS.compactMode]: String(value) }); }
  /** @param value Whether to show the floating NMate assistant. */
  setShowNMate(value: boolean): void       { this.showNMate.set(value);    this._save({ [SETTING_KEYS.showNMate]: String(value) }); }

  // ── Derived helpers ─────────────────────────────────────────────────────────

  /** The Angular date-part pattern for the current date-format preference. */
  datePattern(): string { return DATE_FORMAT_PATTERN[this.dateFormat()]; }

  /**
   * The Angular `formatDate` offset for the preferred zone **at a given instant**.
   * @param at The instant being formatted; defaults to now.
   * @returns An offset like `"+0700"`, or undefined to use the browser's own zone.
   * @remarks
   * Resolved per instant, never stored: a zone's offset changes with daylight saving, so the same
   * Berlin preference must yield +0100 for a January date and +0200 for a July one. Callers that
   * format a specific date should pass it.
   */
  timezoneOffset(at?: Date): string | undefined { return offsetFor(this.timezone(), at); }

  /** The effective zone id — the preference, or the browser's zone when unset. */
  effectiveTimezone(): string { return this.timezone() || browserTimezone(); }

  /**
   * Applies a zone value received from the server, migrating the legacy fixed-offset ids.
   * @param raw The stored value: an IANA id, a legacy `utc7`-style id, or empty for browser local.
   * @remarks
   * Old rows hold values like `"utc7"`. Mapping them on read (and re-saving as IANA) upgrades each
   * user the first time they load, so no data migration is needed for a key-value settings table.
   */
  private setTimezoneFromServer(raw: string): void {
    const migrated = LEGACY_TIMEZONE_IDS[raw];
    this.timezone.set(migrated ?? raw);
    // Re-save so the legacy value disappears for good rather than being re-migrated every load.
    if (migrated) this._save({ [SETTING_KEYS.timezone]: migrated });
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  /** Fire-and-forget PUT to the settings endpoint; ignores failures (localStorage is source of truth during session). */
  private _save(patch: Record<string, string | null>): void {
    this.http.put(this.apiUrl, { settings: patch })
      .pipe(catchError(() => of(null)))
      .subscribe();
  }

  /** Reads a boolean from localStorage; returns {@param defaultValue} if missing. */
  private _loadBool(key: string, defaultValue: boolean): boolean {
    const v = readStorage(key);
    return v === null ? defaultValue : v === 'true';
  }
}
