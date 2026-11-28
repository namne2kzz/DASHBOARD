/**
 * localStorage access that cannot throw.
 *
 * Plain `localStorage` throws in private-browsing modes, when the user blocks site data, and
 * when the origin is over quota. Several of these reads run in field initializers on the
 * startup path, where an exception means a blank app rather than a forgotten preference.
 * Reading returns the fallback and writing is dropped, so the app still runs — it just does
 * not remember anything between visits.
 */

/**
 * Reads a key, returning null when storage is unreadable.
 * @param key Full storage key.
 * @returns The stored string, or null when missing or unreadable.
 */
export function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

/**
 * Writes a key, doing nothing when storage is unwritable.
 * @param key Full storage key.
 * @param value Value to store.
 */
export function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Blocked or full — the value is simply not remembered.
  }
}

/**
 * Removes a key, doing nothing when storage is unreachable.
 * @param key Full storage key.
 */
export function removeStorage(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Nothing to remove if storage cannot be reached.
  }
}
