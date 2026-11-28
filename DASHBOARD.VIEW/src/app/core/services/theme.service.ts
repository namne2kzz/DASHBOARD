import { effect, inject, Injectable, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { StorageKeys } from '../constants/storage-keys.constant';
import { readStorage, writeStorage } from '../utils/safe-storage.util';

export type Theme = 'dark' | 'light';

/**
 * The accent hue, chosen independently of light/dark. Each one is a palette in
 * styles.css under html[data-accent]; the surfaces come from the theme.
 */
export type Accent = 'blue' | 'teal' | 'slate';

/** Accent options in the order the settings UI lists them. */
export const ACCENTS: ReadonlyArray<{ value: Accent; label: string; description: string }> = [
  { value: 'blue',  label: 'Blue',  description: 'The instrument default' },
  { value: 'teal',  label: 'Teal',  description: 'Infrastructure and developer tooling' },
  { value: 'slate', label: 'Slate', description: 'Quietest — only data carries colour' },
];

const DEFAULT_ACCENT: Accent = 'blue';

/** Narrows an unknown stored value to a known accent. @param v Raw value. @returns A valid accent. */
function toAccent(v: string | null): Accent {
  return ACCENTS.some(a => a.value === v) ? (v as Accent) : DEFAULT_ACCENT;
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<Theme>(
    (readStorage(StorageKeys.theme) as Theme | null) ?? 'dark',
  );

  /** The selected accent hue. Persisted separately from the light/dark theme. */
  readonly accent = signal<Accent>(toAccent(readStorage(StorageKeys.accent)));

  readonly accents = ACCENTS;

  constructor() {
    effect(() => {
      const t = this.theme();
      writeStorage(StorageKeys.theme, t);
      // Design tokens in styles.css switch on html[data-theme].
      this.document.documentElement.setAttribute('data-theme', t);
    });

    effect(() => {
      const a = this.accent();
      writeStorage(StorageKeys.accent, a);
      // Accent palettes in styles.css switch on html[data-accent], independently
      // of the theme, so the two can be combined freely.
      this.document.documentElement.setAttribute('data-accent', a);
    });
  }

  /** Toggle between dark and light themes. */
  toggle(): void {
    this.theme.update(t => (t === 'dark' ? 'light' : 'dark'));
  }

  /**
   * Switches to a specific theme.
   * @param theme The theme to apply.
   */
  set(theme: Theme): void {
    this.theme.set(theme);
  }

  /**
   * Switches to a specific accent hue.
   * @param accent The accent to apply.
   */
  setAccent(accent: Accent): void {
    this.accent.set(accent);
  }
}
