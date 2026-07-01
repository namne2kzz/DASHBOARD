import { inject, Injectable, LOCALE_ID } from '@angular/core';
import { formatDate } from '@angular/common';

@Injectable({ providedIn: 'root' })
export class DateTimeService {
  private readonly locale = inject(LOCALE_ID);

  /** Default display format used across the app. */
  static readonly DEFAULT_FORMAT = 'MMM d, y · h:mm a';

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
   * Formats a UTC ISO string as a localised display string.
   * @param utc UTC ISO string from the backend.
   * @param format Angular date format string. Defaults to 'MMM d, y · h:mm a'.
   * @returns Formatted local-time string, or empty string if input is falsy.
   */
  format(utc: string | null | undefined, format = DateTimeService.DEFAULT_FORMAT): string {
    if (!utc) return '';
    return formatDate(this.normalise(utc), format, this.locale);
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
    return this.format(utc, 'MMM d, y');
  }
}
