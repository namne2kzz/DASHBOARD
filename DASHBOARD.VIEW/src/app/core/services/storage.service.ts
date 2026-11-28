import { Injectable } from '@angular/core';

const APP_PREFIX = 'dashboard.';

/**
 * Thin wrapper over localStorage.
 *
 * Every access is guarded: a browser in private mode, with site data blocked, or over quota
 * throws on plain `localStorage` use, and these calls sit on the auth path — an unguarded
 * throw there takes down sign-in rather than losing a preference. A failed write degrades to
 * "value not remembered", never to a broken app.
 */
@Injectable({ providedIn: 'root' })
export class StorageService {
  private key(k: string): string {
    return `${APP_PREFIX}${k}`;
  }

  /** Serializes and stores a value in localStorage. Silently does nothing when storage is unavailable. */
  set<T>(key: string, value: T): void {
    try {
      localStorage.setItem(this.key(key), JSON.stringify(value));
    } catch {
      // Storage blocked or full — the value simply is not remembered.
    }
  }

  /** Retrieves and deserializes a value; returns null if missing, corrupt, or unreadable. */
  get<T>(key: string): T | null {
    const raw = this.getString(key);
    if (raw === null) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  }

  /** Returns a raw string value without JSON parsing, or null when unreadable. */
  getString(key: string): string | null {
    try {
      return localStorage.getItem(this.key(key));
    } catch {
      return null;
    }
  }

  /** Stores a raw string value without JSON serialization. Silently does nothing when storage is unavailable. */
  setString(key: string, value: string): void {
    try {
      localStorage.setItem(this.key(key), value);
    } catch {
      // See set().
    }
  }

  /** Removes a single entry. */
  remove(key: string): void {
    try {
      localStorage.removeItem(this.key(key));
    } catch {
      // Nothing to remove if storage cannot be reached.
    }
  }

  /** Removes all entries that share this app's prefix. */
  clear(): void {
    try {
      Object.keys(localStorage)
        .filter(k => k.startsWith(APP_PREFIX))
        .forEach(k => localStorage.removeItem(k));
    } catch {
      // See remove().
    }
  }

  /** Returns true if the key exists in localStorage. */
  has(key: string): boolean {
    return this.getString(key) !== null;
  }
}
