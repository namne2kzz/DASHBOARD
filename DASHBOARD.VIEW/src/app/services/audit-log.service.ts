import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { AuditLogEntry, AuditLogFilters, PagedResponse } from '../models/audit-log.model';

/** Loads the repository audit log with server-side filtering and incremental paging. */
@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx    = inject(RepositoryContextService);
  private readonly destroyRef = inject(DestroyRef);

  private static readonly PAGE_SIZE = 30;

  private readonly _entries    = signal<AuditLogEntry[]>([]);
  private readonly _loading    = signal(false);
  private readonly _error      = signal(false);
  private readonly _page       = signal(1);
  private readonly _hasMore    = signal(false);
  private readonly _totalCount = signal(0);

  /** Loaded audit-log entries (accumulated across pages). */
  readonly entries    = this._entries.asReadonly();
  /** True while a request is in flight. */
  readonly loading    = this._loading.asReadonly();
  /** True when the last request failed. */
  readonly error      = this._error.asReadonly();
  /** True when more pages remain. */
  readonly hasMore    = this._hasMore.asReadonly();
  /** Total number of matching entries across all pages. */
  readonly totalCount = this._totalCount.asReadonly();

  private _filters: AuditLogFilters = { authorId: '', from: '', to: '', category: '' };

  /** Applies new filters and reloads from the first page. @param filters The active filter selection. */
  applyFilters(filters: AuditLogFilters): void {
    this._filters = filters;
    this.refresh();
  }

  /** Reloads the audit log from the first page using the current filters. */
  refresh(): void {
    this._page.set(1);
    this._entries.set([]);
    this._fetch(1, false);
  }

  /** Loads and appends the next page, if any. */
  loadMore(): void {
    if (this._loading() || !this._hasMore()) return;
    const next = this._page() + 1;
    this._page.set(next);
    this._fetch(next, true);
  }

  private _fetch(page: number, append: boolean): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', AuditLogService.PAGE_SIZE);

    if (this._filters.authorId)   params = params.set('authorId', this._filters.authorId);
    if (this._filters.category)   params = params.set('category', this._filters.category);
    if (this._filters.from)       params = params.set('from', `${this._filters.from}T00:00:00Z`);
    if (this._filters.to)         params = params.set('to', `${this._filters.to}T23:59:59Z`);

    this._loading.set(true);
    this._error.set(false);
    this.http
      .get<PagedResponse<AuditLogEntry>>(`${environment.apiBaseUrl}/repositories/${repoId}/audit-log`, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this._entries.update(cur => append ? [...cur, ...res.items] : res.items);
          this._hasMore.set(res.hasNextPage);
          this._totalCount.set(res.totalCount);
          this._loading.set(false);
        },
        error: () => {
          this._error.set(true);
          this._loading.set(false);
        },
      });
  }
}
