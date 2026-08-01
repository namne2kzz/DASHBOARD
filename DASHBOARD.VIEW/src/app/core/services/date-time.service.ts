import { inject, Injectable, LOCALE_ID } from '@angular/core';
import { formatDate } from '@angular/common';
import { PreferencesService } from './preferences.service';

@Injectable({ providedIn: 'root' })
export class DateTimeService {
  private readonly locale = inject(LOCALE_ID);
  private readonly prefs  = inject(PreferencesService);

  /** Time portion appended to the preferred date pattern for the default display format. */
  private static readonly TIME_SUFFIX = ' · h:mm a';

  /** The full default display format, derived from the user's date-format preference. */
  private defaultFormat(): string {
    return this.prefs.datePattern() + DateTimeService.TIME_SUFFIX;
  }

  /**
   * Normalises a UTC ISO string so it is always parsed as UTC.
   * Appends 'Z' if the string has no timezone designator.
   */
  private normalise(utc: string): string {
    return /[Zz]$|[+-]\d{2}:?\d{2}$/.test(utc) ? utc : `${utc}Z`;
  }

  /**
   * Converts a UTC ISO string to a local Date object.
   * @param utc UTC ISO string from the backend.
   * @returns Local Date, or null if the input is falsy.
   */
  toLocal(utc: string | null | undefined): Date | null {
    if (!utc) return null;
    return new Date(this.normalise(utc));
  }

  /**
   * Formats a UTC ISO string as a display string honouring the user's date-format and timezone
   * preferences. When no explicit format is given, the preferred numeric date pattern is used.
   * The preferred timezone is always applied (falls back to the browser's local zone).
   * @param utc UTC ISO string from the backend.
   * @param format Optional explicit Angular date format string; omit to use the preferred default.
   * @returns Formatted string, or empty string if input is falsy.
   */
  format(utc: string | null | undefined, format?: string): string {
    if (!utc) return '';
    return formatDate(this.normalise(utc), format ?? this.defaultFormat(), this.locale, this.prefs.timezoneOffset());
  }

  /**
   * Returns a short relative label ('Today', 'Yesterday', or the formatted date).
   * Useful for grouping lists by date.
   * @param utc UTC ISO string from the backend.
   */
  relativeDay(utc: string | null | undefined): string {
    const date = this.toLocal(utc);
    if (!date) return '';
    const today = new Date();
    const diffDays = Math.floor(
      (today.setHours(0, 0, 0, 0) - date.setHours(0, 0, 0, 0)) / 86_400_000,
    );
    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    return this.format(utc, this.prefs.datePattern());
  }
}
