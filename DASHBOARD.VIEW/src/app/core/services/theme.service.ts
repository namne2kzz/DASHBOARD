import { effect, inject, Injectable, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { StorageKeys } from '../constants/storage-keys.constant';

export type Theme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<Theme>(
    (localStorage.getItem(StorageKeys.theme) as Theme | null) ?? 'dark',
  );

  constructor() {
    effect(() => {
      const t = this.theme();
      localStorage.setItem(StorageKeys.theme, t);
      // Design tokens in styles.css switch on html[data-theme].
      this.document.documentElement.setAttribute('data-theme', t);
    });
  }

  /** Toggle between dark and light themes. */
  toggle(): void {
    this.theme.update(t => (t === 'dark' ? 'light' : 'dark'));
  }
}
