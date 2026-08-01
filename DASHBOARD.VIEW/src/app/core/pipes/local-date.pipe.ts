import { Pipe, PipeTransform } from '@angular/core';
import { DateTimeService } from '../services/date-time.service';

// Impure so dates re-render live when the user changes their date-format / timezone preference.
@Pipe({ name: 'localDate', standalone: true, pure: false })
export class LocalDatePipe implements PipeTransform {
  constructor(private readonly dt: DateTimeService) {}

  /**
   * Converts a UTC ISO string to a formatted local-time string.
   * @param value UTC ISO string from the backend.
   * @param format Angular date format string. Defaults to 'MMM d, y · h:mm a'.
   */
  transform(value: string | null | undefined, format?: string): string {
    return this.dt.format(value, format);
  }
}
