import { Injectable } from '@angular/core';

const APP_PREFIX = 'dashboard.';

@Injectable({ providedIn: 'root' })
export class StorageService {
  private key(k: string): string {
    return `${APP_PREFIX}${k}`;
  }

  /** Serializes and stores a value in localStorage. */
  set<T>(key: string, value: T): void {
    localStorage.setItem(this.key(key), JSON.stringify(value));
  }

  /** Retrieves and deserializes a value; returns null if missing or corrupt. */
  get<T>(key: string): T | null {
    const raw = localStorage.getItem(this.key(key));
    if (raw === null) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  }

  /** Returns a raw string value without JSON parsing. */
  getString(key: string): string | null {
    return localStorage.getItem(this.key(key));
  }

  /** Stores a raw string value without JSON serialization. */
  setString(key: string, value: string): void {
    localStorage.setItem(this.key(key), value);
  }

  /** Removes a single entry. */
  remove(key: string): void {
    localStorage.removeItem(this.key(key));
  }

  /** Removes all entries that share this app's prefix. */
  clear(): void {
    Object.keys(localStorage)
      .filter(k => k.startsWith(APP_PREFIX))
      .forEach(k => localStorage.removeItem(k));
  }

  /** Returns true if the key exists in localStorage. */
  has(key: string): boolean {
    return localStorage.getItem(this.key(key)) !== null;
  }
}
