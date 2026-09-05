import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, EMPTY, of, Subject, switchMap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { SearchResultGroup, SearchResultItem, SearchResultKind } from '../models/search.model';

/** Backs the global command-palette search: open state, debounced querying, and grouped results. */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx    = inject(RepositoryContextService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _open    = signal(false);
  private readonly _term    = signal('');
  private readonly _results = signal<SearchResultItem[]>([]);
  private readonly _loading = signal(false);

  /** Whether the search palette is open. */
  readonly open    = this._open.asReadonly();
  /** The current search term. */
  readonly term    = this._term.asReadonly();
  /** True while a search request is in flight. */
  readonly loading = this._loading.asReadonly();

  private readonly _labels: Record<SearchResultKind, string> = {
    task:    'Work items',
    backlog: 'Backlog',
  };
  private readonly _order: SearchResultKind[] = ['task', 'backlog'];

  /** Results grouped by kind, in a stable display order. */
  readonly groups = computed<SearchResultGroup[]>(() => {
    const items = this._results();
    return this._order
      .map(kind => ({ kind, label: this._labels[kind], items: items.filter(i => i.kind === kind) }))
      .filter(g => g.items.length > 0);
  });

  private readonly _query$ = new Subject<string>();

  constructor() {
    this._query$
      .pipe(
        debounceTime(200),
        distinctUntilChanged(),
        switchMap(term => {
          const repoId = this.repoCtx.selectedRepoId();
          if (!repoId || term.trim().length < 2) {
            this._results.set([]);
            this._loading.set(false);
            return EMPTY;
          }
          this._loading.set(true);
          return this.http
            .get<SearchResultItem[]>(
              `${environment.apiBaseUrl}/repositories/${repoId}/search`,
              { params: { q: term.trim() } })
            .pipe(catchError(() => of<SearchResultItem[]>([])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(results => {
        this._results.set(results);
        this._loading.set(false);
      });
  }

  /** Opens the palette with a cleared term and results. */
  openPalette(): void {
    this._term.set('');
    this._results.set([]);
    this._loading.set(false);
    this._open.set(true);
  }

  /** Closes the palette. */
  close(): void {
    this._open.set(false);
  }

  /** Updates the search term and triggers a debounced query. @param value The new term. */
  setTerm(value: string): void {
    this._term.set(value);
    this._query$.next(value);
  }
}
