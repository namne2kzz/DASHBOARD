import { inject, Injectable } from '@angular/core';
import { DialogService } from '../dialog/dialog.service';
import { ConfirmComponent, ConfirmOptions, ConfirmType } from './confirm.component';

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(DialogService);

  /**
   * Opens the styled confirm dialog and returns a Promise that resolves to
   * `true` (user confirmed) or `false` (cancelled / dismissed).
   *
   * **Shorthand** — pass a plain string title and optional description:
   * ```ts
   * const ok = await this.confirm.ask('Delete sprint?', 'This cannot be undone.', 'danger');
   * ```
   *
   * **Full options** — resource-key mode or full customisation:
   * ```ts
   * const ok = await this.confirm.ask({ title: 'Archive?', type: 'warning' });
   * ```
   *
   * @param optionsOrTitle `ConfirmOptions` object OR a plain-string title.
   * @param description    Body copy (only used when first param is a string).
   * @param type           Visual tone (only used when first param is a string). Default: 'danger'.
   * @returns `true` when the user clicks Confirm; `false` otherwise.
   */
  async ask(
    optionsOrTitle: ConfirmOptions | string,
    description?: string,
    type: ConfirmType = 'danger',
  ): Promise<boolean> {
    const opts: ConfirmOptions =
      typeof optionsOrTitle === 'string'
        ? { title: optionsOrTitle, description, type }
        : optionsOrTitle;

    const ref = this.dialog.open<ConfirmComponent, ConfirmOptions, boolean>(
      ConfirmComponent,
      { data: opts, width: '24rem', closable: false },
    );
    return (await ref.closed) ?? false;
  }
}
