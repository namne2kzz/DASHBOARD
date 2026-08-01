import { effect, Injectable, signal } from '@angular/core';
import { StorageKeys } from '../constants/storage-keys.constant';

/** Numeric date ordering the app uses when formatting dates. */
export type DateFormat = 'dmy' | 'mdy' | 'ymd';

/** A selectable display timezone. Empty string means the browser's local zone. */
export type TimezoneId = '' | 'utc7' | 'utc0' | 'utc-5' | 'utc9';

/** Maps a {@link DateFormat} to the Angular date-part pattern used by DateTimeService. */
export const DATE_FORMAT_PATTERN: Record<DateFormat, string> = {
  dmy: 'dd/MM/y',
  mdy: 'MM/dd/y',
  ymd: 'y-MM-dd',
};

/** Maps a {@link TimezoneId} to an Angular `formatDate` timezone offset (undefined = browser local). */
export const TIMEZONE_OFFSET: Record<TimezoneId, string | undefined> = {
  '':     undefined,
  utc7:   '+0700',
  utc0:   '+0000',
  'utc-5': '-0500',
  utc9:   '+0900',
};

/**
 * Persists client-side display preferences (date format, timezone) in localStorage — the same
 * approach as {@link ThemeService}. Consumed by {@link DateTimeService} so every date rendered
 * through it reflects the user's choice.
 */
@Injectable({ providedIn: 'root' })
export class PreferencesService {
  readonly dateFormat = signal<DateFormat>(
    (localStorage.getItem(StorageKeys.dateFormat) as DateFormat | null) ?? 'dmy',
  );

  readonly timezone = signal<TimezoneId>(
    (localStorage.getItem(StorageKeys.timezone) as TimezoneId | null) ?? '',
  );

  constructor() {
    effect(() => localStorage.setItem(StorageKeys.dateFormat, this.dateFormat()));
    effect(() => localStorage.setItem(StorageKeys.timezone, this.timezone()));
  }

  /** Sets the preferred numeric date ordering. @param value The date format. */
  setDateFormat(value: DateFormat): void { this.dateFormat.set(value); }

  /** Sets the preferred display timezone. @param value The timezone id ('' = local). */
  setTimezone(value: TimezoneId): void { this.timezone.set(value); }

  /** The Angular date-part pattern for the current date-format preference. */
  datePattern(): string { return DATE_FORMAT_PATTERN[this.dateFormat()]; }

  /** The Angular `formatDate` timezone offset for the current preference (undefined = local). */
  timezoneOffset(): string | undefined { return TIMEZONE_OFFSET[this.timezone()]; }
}
