import { Injectable, signal, computed } from '@angular/core';
import { Observable, finalize } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly _count = signal(0);

  /** True while any active loading request is in flight. */
  readonly isLoading = computed(() => this._count() > 0);

  /** Wraps an Observable: shows the overlay while the source is active, hides on complete/error. */
  run<T>(source$: Observable<T>): Observable<T> {
    this.show();
    return source$.pipe(finalize(() => this.hide()));
  }

  /** Manually increments the loading counter. */
  show(): void {
    this._count.update(n => n + 1);
  }

  /** Manually decrements the loading counter. */
  hide(): void {
    this._count.update(n => Math.max(0, n - 1));
  }
}
