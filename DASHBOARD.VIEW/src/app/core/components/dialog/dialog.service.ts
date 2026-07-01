import { Injectable, signal, Type } from '@angular/core';
import { DialogConfig, DialogEntry, DialogRef } from '../../../models/dialog.model';

let nextId = 0;

@Injectable({ providedIn: 'root' })
export class DialogService {
  readonly dialogs = signal<DialogEntry[]>([]);

  /**
   * Opens a component inside the dialog shell.
   * @param component The component class to render as dialog content.
   * @param config title, data, width, height, mode ('normal' | 'max'), closable.
   * @returns DialogRef whose .closed Promise resolves with the result when closed.
   */
  open<C, D = unknown, R = unknown>(
    component: Type<C>,
    config: DialogConfig<D> = {},
  ): DialogRef<R> {
    const id = ++nextId;
    let resolveFn!: (result: R | undefined) => void;
    const closed = new Promise<R | undefined>(res => (resolveFn = res));

    const entry: DialogEntry = {
      id,
      component: component as Type<unknown>,
      config: {
        title:    config.title,
        data:     config.data,
        width:    config.width  ?? '32rem',
        height:   config.height,
        mode:     config.mode     ?? 'normal',
        closable: config.closable ?? true,
      },
      resolve: resolveFn as (r: unknown) => void,
    };

    this.dialogs.update(list => [...list, entry]);

    return {
      close: (result?: R) => this._close(id, result),
      closed,
    };
  }

  /** @internal Called by DialogComponent to close an entry. */
  _close(id: number, result?: unknown): void {
    const entry = this.dialogs().find(d => d.id === id);
    if (entry) {
      entry.resolve(result);
      this.dialogs.update(list => list.filter(d => d.id !== id));
    }
  }

  /** Toggles a dialog between its configured size and full-screen 'max' mode. @param id Dialog entry ID. */
  toggleMaximize(id: number): void {
    this.dialogs.update(list => list.map(d =>
      d.id === id
        ? { ...d, config: { ...d.config, mode: d.config.mode === 'max' ? 'normal' : 'max' } }
        : d,
    ));
  }
}
