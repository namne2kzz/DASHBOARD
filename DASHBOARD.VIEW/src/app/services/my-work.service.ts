import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { environment } from '../../environments/environment';
import { MyWorkItemApiDto } from '../models/my-work.model';

/** Loads and holds the current user's cross-repository open work queue. */
@Injectable({ providedIn: 'root' })
export class MyWorkService {
  private readonly http       = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly baseUrl    = `${environment.apiBaseUrl}/my-work`;

  private readonly _items   = signal<MyWorkItemApiDto[]>([]);
  private readonly _loading = signal(false);
  private readonly _error   = signal(false);

  /** All open work items assigned to the current user. */
  readonly items   = this._items.asReadonly();
  /** True while a fetch is in flight. */
  readonly loading = this._loading.asReadonly();
  /** True when the last fetch failed. */
  readonly error   = this._error.asReadonly();

  /** Total number of open assigned items. */
  readonly totalCount = computed(() => this._items().length);
  /** Number of items currently in the Active state. */
  readonly activeCount = computed(() => this._items().filter(i => i.state === 3).length);
  /** Number of items currently in the In Review state. */
  readonly inReviewCount = computed(() => this._items().filter(i => i.state === 4).length);
  /** Distinct repositories represented in the queue. */
  readonly repoCount = computed(() => new Set(this._items().map(i => i.repositoryId)).size);

  /** Fetches the user's assigned open work items, replacing the current list. */
  refresh(): void {
    this._loading.set(true);
    this._error.set(false);
    this.http
      .get<MyWorkItemApiDto[]>(this.baseUrl)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: items => {
          this._items.set(items);
          this._loading.set(false);
        },
        error: () => {
          this._error.set(true);
          this._loading.set(false);
        },
      });
  }
}
