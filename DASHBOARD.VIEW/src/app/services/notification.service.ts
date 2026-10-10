import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AppNotification } from '../models/notification.model';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

const BASE = `${environment.apiBaseUrl}/notifications`;
const RECONNECT_DELAYS = [2_000, 5_000, 10_000, 30_000];

/**
 * Manages in-app notifications: opens a Server-Sent Events connection while the
 * user is logged in, feeds incoming pushes into a signal, and exposes list/mark-read
 * operations backed by the REST API.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private readonly http    = inject(HttpClient);
  private readonly storage = inject(StorageService);
  private readonly router  = inject(Router);

  private readonly _notifications = signal<AppNotification[]>([]);
  private _es: EventSource | null = null;
  private _reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private _reconnectAttempt = 0;
  private _destroyed = false;

  /** All notifications, newest first. */
  readonly notifications = this._notifications.asReadonly();

  /** Number of unread notifications — drives the badge. */
  readonly unreadCount = computed(() =>
    this._notifications().filter(n => !n.isRead).length
  );

  // ── SSE lifecycle ────────────────────────────────────────────────────────

  /** Opens the SSE stream. Call once after login. */
  connect(): void {
    this._destroyed = false;
    this._reconnectAttempt = 0;
    this._fetchRecent();
    this._openStream();
  }

  /** Closes the SSE stream and clears state. Call on logout. */
  disconnect(): void {
    this._destroyed = true;
    this._clearReconnect();
    this._closeStream();
    this._notifications.set([]);
  }

  // ── REST operations ──────────────────────────────────────────────────────

  /** Marks one notification as read and updates the local signal. */
  async markRead(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${BASE}/${id}/read`, {}));
    this._notifications.update(list =>
      list.map(n => n.id === id ? { ...n, isRead: true } : n)
    );
  }

  /** Marks all notifications as read. */
  async markAllRead(): Promise<void> {
    await firstValueFrom(this.http.post(`${BASE}/read-all`, {}));
    this._notifications.update(list => list.map(n => ({ ...n, isRead: true })));
  }

  /** Navigates to the notification's URL (if any) and marks it read. */
  async open(n: AppNotification): Promise<void> {
    if (!n.isRead) await this.markRead(n.id);
    if (n.url) await this.router.navigateByUrl(n.url);
  }

  ngOnDestroy(): void {
    this.disconnect();
  }

  // ── Private ──────────────────────────────────────────────────────────────

  private _fetchRecent(): void {
    this.http.get<AppNotification[]>(BASE).subscribe({
      next: list => this._notifications.set(list),
      error: () => { /* non-critical */ },
    });
  }

  private _openStream(): void {
    this._closeStream();

    // EventSource does not send the Authorization header, so we pass the token
    // as a query param — the backend reads it from the standard JWT bearer location,
    // or we use a cookie-based approach. Here we append the raw token so the
    // existing JWT middleware can pick it up via a custom query-param check.
    const token = this.storage.get<string>(StorageKeys.accessToken);
    if (!token) return;

    this._es = new EventSource(`${BASE}/stream?access_token=${encodeURIComponent(token)}`);

    this._es.addEventListener('notification', (e: MessageEvent) => {
      try {
        const n: AppNotification = JSON.parse(e.data);
        this._notifications.update(list => [n, ...list].slice(0, 50));
        this._reconnectAttempt = 0;
      } catch { /* malformed event — ignore */ }
    });

    this._es.onerror = () => {
      this._closeStream();
      if (!this._destroyed) this._scheduleReconnect();
    };
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
      if (!this._destroyed) this._openStream();
    }, delay);
  }

  private _clearReconnect(): void {
    if (this._reconnectTimer !== null) {
      clearTimeout(this._reconnectTimer);
      this._reconnectTimer = null;
    }
  }
}
