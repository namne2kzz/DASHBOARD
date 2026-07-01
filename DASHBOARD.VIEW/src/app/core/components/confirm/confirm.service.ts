import { inject, Injectable } from '@angular/core';
import { DialogService } from '../dialog/dialog.service';
import { ConfirmComponent, ConfirmOptions } from './confirm.component';

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(DialogService);

  /**
   * Opens a confirm dialog and returns a Promise that resolves to true (confirmed) or false (cancelled).
   * @param options Optional resource keys and danger flag to customize the dialog.
   */
  async ask(options: ConfirmOptions = {}): Promise<boolean> {
    const ref = this.dialog.open<ConfirmComponent, ConfirmOptions, boolean>(
      ConfirmComponent,
      { data: options, width: '24rem', closable: false },
    );
    return (await ref.closed) ?? false;
  }
}
