import { inject, Injectable } from '@angular/core';
import { DialogService } from '../dialog/dialog.service';
import { PromptComponent, PromptOptions } from './prompt.component';

/**
 * Opens the styled prompt (text-input) dialog and returns a Promise that resolves
 * to the trimmed string the user typed, or `null` when they cancelled or left it empty.
 *
 * Usage:
 * ```ts
 * const name = await this.prompt.ask({ title: 'Clone role', label: 'New name', defaultValue: 'Role (Copy)' });
 * if (!name) return;
 * ```
 */
@Injectable({ providedIn: 'root' })
export class PromptService {
  private readonly dialog = inject(DialogService);

  /**
   * Opens the prompt dialog and awaits the user's input.
   * @param options Title, label, default value, and button labels.
   * @returns The trimmed string typed by the user, or `null` if cancelled / empty.
   */
  async ask(options: PromptOptions): Promise<string | null> {
    const ref = this.dialog.open<PromptComponent, PromptOptions, string | null>(
      PromptComponent,
      { data: options, width: '26rem', closable: false },
    );
    return (await ref.closed) ?? null;
  }
}
