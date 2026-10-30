import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { backgroundRequest } from '../models/http-context.model';
import { AppNotification, NotificationCategory, NotificationPage } from '../models/notification.model';

const BASE = `${environment.apiBaseUrl}/notifications`;
const RECONNECT_DELAYS = [2_000, 5_000, 10_000, 30_000];
const PAGE_SIZE = 20;

/**
 * Manages in-app notifications: opens a Server-Sent Events connection while the
 * user is logged in, feeds incoming pushes into a signal, and exposes list/mark-read
 * operations backed by the REST API.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private readonly http    = inject(HttpClient);
  private readonly router  = inject(Router);

  private readonly _notifications = signal<AppNotification[]>([]);
  private readonly _serverUnread  = signal(0);
  private readonly _hasMore       = signal(false);
  private readonly _loading       = signal(false);
  private readonly _filter        = signal<NotificationCategory | null>(null);
  private _es: EventSource | null = null;
  private _reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private _reconnectAttempt = 0;
  private _destroyed = false;

  /** All loaded notifications, newest first. */
  readonly notifications = this._notifications.asReadonly();

  /** Whether older notifications exist beyond the loaded page. */
  readonly hasMore = this._hasMore.asReadonly();

  /** Whether a page fetch is in flight. */
  readonly loading = this._loading.asReadonly();

  /** The active category filter (null = all). */
  readonly filter = this._filter.asReadonly();

  /** Number of unread notifications — drives the badge. Uses the server's count (whole inbox, not just loaded page). */
  readonly unreadCount = this._serverUnread.asReadonly();

  // ── SSE lifecycle ────────────────────────────────────────────────────────

  /** Opens the SSE stream. Call once after login. */
  connect(): void {
    this._destroyed = false;
    this._reconnectAttempt = 0;
    this._openStream();
  }

  /** Closes the SSE stream and clears state. Call on logout. */
  disconnect(): void {
    this._destroyed = true;
    this._clearReconnect();
    this._closeStream();
    this._notifications.set([]);
    this._serverUnread.set(0);
    this._hasMore.set(false);
    this._filter.set(null);
  }

  // ── REST operations ──────────────────────────────────────────────────────

  /** Marks one notification as read and updates the local signal + unread count. */
  async markRead(id: string): Promise<void> {
    const wasUnread = this._notifications().some(n => n.id === id && !n.isRead);
    await firstValueFrom(this.http.post(`${BASE}/${id}/read`, {}));
    this._notifications.update(list =>
      list.map(n => n.id === id ? { ...n, isRead: true } : n)
    );
    if (wasUnread) this._serverUnread.update(c => Math.max(0, c - 1));
  }

  /** Marks all notifications as read. */
  async markAllRead(): Promise<void> {
    await firstValueFrom(this.http.post(`${BASE}/read-all`, {}));
    this._notifications.update(list => list.map(n => ({ ...n, isRead: true })));
    this._serverUnread.set(0);
  }

  /** Navigates to the notification's URL (if any) and marks it read. */
  async open(n: AppNotification): Promise<void> {
    if (!n.isRead) await this.markRead(n.id);
    if (n.url) await this.router.navigateByUrl(n.url);
  }

  /** Sets the category filter and reloads the first page. @param category Category or null for all. */
  setFilter(category: NotificationCategory | null): void {
    this._filter.set(category);
    this._loadPage(0, true);
  }

  /** Loads the next page of notifications (append). */
  loadMore(): void {
    if (this._loading() || !this._hasMore()) return;
    this._loadPage(this._notifications().length, false);
  }

  /** Reloads the first page with the current filter (e.g. after opening the panel). */
  refresh(): void {
    this._loadPage(0, true);
  }

  ngOnDestroy(): void {
    this.disconnect();
  }

  // ── Private ──────────────────────────────────────────────────────────────

  private _fetchRecent(): void {
    // Background: this runs on connect and on every SSE reconnect, neither of which the user
    // asked for. A 401 here must not bounce them out of the page they are working on.
    this._loadPage(0, true, true);
  }

  /**
   * Fetches one page of notifications.
   * @param skip Offset into the list. @param replace Whether to replace rather than append.
   * @param background True for polls and reconnect catch-up reads, so a 401 does not end the session.
   */
  private _loadPage(skip: number, replace: boolean, background = false): void {
    this._loading.set(true);
    let params = new HttpParams().set('skip', skip).set('take', PAGE_SIZE);
    const filter = this._filter();
    if (filter) params = params.set('category', filter);

    const options = background ? { params, context: backgroundRequest() } : { params };

    this.http.get<NotificationPage>(BASE, options).subscribe({
      next: page => {
        this._notifications.update(list => replace ? page.items : [...list, ...page.items]);
        this._hasMore.set(page.hasMore);
        this._serverUnread.set(page.unreadCount);
        this._loading.set(false);
      },
      error: () => this._loading.set(false),
    });
  }

  private _openStream(): void {
    this._closeStream();

    // Auth now rides on the HttpOnly cookie. EventSource sends it automatically when opened with
    // { withCredentials: true } (the backend CORS policy allows credentials), so there is no token
    // to place in the URL — JwtBearerEvents.OnMessageReceived reads the access cookie instead.
    this._es = new EventSource(`${BASE}/stream`, { withCredentials: true });

    // Fetch the current list as soon as the stream is established so the panel is in sync.
    this._fetchRecent();

    this._es.onopen = () => {
      this._reconnectAttempt = 0;
    };

    this._es.addEventListener('notification', (e: MessageEvent) => {
      try {
        const n: AppNotification = JSON.parse(e.data);
        this._serverUnread.update(c => c + 1);
        // Only prepend when it matches the active filter so the visible list stays consistent.
        if (this._matchesFilter(n))
          this._notifications.update(list => [n, ...list]);
      } catch { /* malformed event — ignore */ }
    });

    this._es.onerror = () => {
      this._closeStream();
      if (!this._destroyed) this._scheduleReconnect();
    };
  }

  /** Whether an incoming notification belongs to the active category filter. */
  private _matchesFilter(n: AppNotification): boolean {
    const filter = this._filter();
    if (!filter) return true;
    switch (filter) {
      case 'workitems':  return n.type.startsWith('WorkItem');
      case 'sprints':    return n.type === 'SprintActivated' || n.type === 'SprintClosed';
      case 'membership': return n.type.startsWith('Added') || n.type.startsWith('Removed');
      case 'mentions':   return n.type === 'MentionedInDiscussion';
    }
  }

  private _closeStream(): void {
    this._es?.close();
    this._es = null;
  }

  private _scheduleReconnect(): void {
    this._clearReconnect();
    const delay = RECONNECT_DELAYS[Math.min(this._reconnectAttempt, RECONNECT_DELAYS.length - 1)];
    this._reconnectAttempt++;
    this._reconnectTimer = setTimeout(() => {
      if (!this._destroyed) {
        this._fetchRecent();
        this._openStream();
      }
    }, delay);
  }

  private _clearReconnect(): void {
    if (this._reconnectTimer !== null) {
      clearTimeout(this._reconnectTimer);
      this._reconnectTimer = null;
    }
  }
}
