import { Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';

/**
 * In-memory TTL cache for GET observables, so reopening the same panel or retyping the same search
 * term reuses the previous response instead of issuing an identical request.
 */
@Injectable({ providedIn: 'root' })
export class HttpCacheService {
  private readonly entries = new Map<string, { at: number; value$: Observable<unknown> }>();

  /**
   * Returns a memoised observable for a key, re-running the factory once the entry outlives its TTL.
   * @param key Cache key; must include every parameter that changes the response.
   * @param ttlMs How long an entry stays fresh, in milliseconds.
   * @param factory Source factory, invoked only on a miss.
   * @returns Shared observable replaying the last value to every subscriber.
   */
  get<T>(key: string, ttlMs: number, factory: () => Observable<T>): Observable<T> {
    const hit = this.entries.get(key);
    if (hit && Date.now() - hit.at < ttlMs) return hit.value$ as Observable<T>;

    // refCount stays false so the value survives after the last subscriber unsubscribes — a panel
    // that closes and reopens is exactly the case this cache exists for.
    const value$ = factory().pipe(shareReplay({ bufferSize: 1, refCount: false }));
    this.entries.set(key, { at: Date.now(), value$ });
    return value$;
  }

  /**
   * Drops every entry whose key starts with the prefix. Call this after a mutation that invalidates
   * the cached reads, otherwise the stale value is served until its TTL expires.
   * @param prefix Key prefix to evict.
   */
  invalidate(prefix: string): void {
    for (const key of [...this.entries.keys()]) {
      if (key.startsWith(prefix)) this.entries.delete(key);
    }
  }

  /** Clears the whole cache — used on sign-out so the next user never sees the previous one's data. */
  clear(): void {
    this.entries.clear();
  }
}
