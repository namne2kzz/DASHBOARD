import { HttpClient } from '@angular/common/http';
import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { AboutDto } from '../models/about.model';
import { describeLoadError } from '../utils/http-error.util';

/**
 * Build identity and live dependency status for the About dialog.
 *
 * Loads only when asked: the endpoint probes real services, so it must not run on app start.
 */
@Injectable({ providedIn: 'root' })
export class AboutService {
  private readonly http       = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly error   = signal<string | null>(null);
  readonly info    = signal<AboutDto | null>(null);

  /** Fetches build info and probes every dependency. Safe to call repeatedly; ignores overlapping calls. */
  load(): void {
    if (this.loading()) return;

    this.loading.set(true);
    this.error.set(null);

    this.http
      .get<AboutDto>(`${environment.apiBaseUrl}/system/about`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: info => {
          this.info.set(info);
          this.loading.set(false);
        },
        error: err => {
          this.error.set(describeLoadError(err, 'Failed to load system information.'));
          this.loading.set(false);
        },
      });
  }
}
