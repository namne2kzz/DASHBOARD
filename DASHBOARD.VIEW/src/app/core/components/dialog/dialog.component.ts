import { Component, effect, inject, Injector } from '@angular/core';
import { NgComponentOutlet, NgClass, NgStyle } from '@angular/common';
import { DialogService } from './dialog.service';
import { DialogEntry, DIALOG_REF_TOKEN } from '../../../models/dialog.model';
import { ResourcePipe } from '../../pipes/resource.pipe';

@Component({
  selector: 'app-dialog',
  standalone: true,
  imports: [NgComponentOutlet, NgClass, NgStyle, ResourcePipe],
  templateUrl: './dialog.component.html',
  styleUrl: './dialog.component.scss',
})
export class DialogComponent {
  protected readonly svc      = inject(DialogService);
  protected readonly injector = inject(Injector);

  /** Cached per-dialog injectors keyed by entry ID — prevents NgComponentOutlet from recreating the component on every CD cycle. */
  private readonly injectorCache = new Map<number, Injector>();

  constructor() {
    effect(() => {
      const activeIds = new Set(this.svc.dialogs().map(d => d.id));
      for (const id of this.injectorCache.keys()) {
        if (!activeIds.has(id)) this.injectorCache.delete(id);
      }
    });
  }

  protected close(entry: DialogEntry): void {
    if (entry.config.closable) this.svc._close(entry.id);
  }

  /** Toggles the dialog between its configured size and full-screen. @param entry The dialog entry to resize. */
  protected toggleMaximize(entry: DialogEntry): void {
    this.svc.toggleMaximize(entry.id);
  }

  protected trackById(_: number, e: DialogEntry): number { return e.id; }

  protected panelClass(entry: DialogEntry): string {
    return entry.config.mode === 'max'
      ? 'w-screen h-screen rounded-none'
      : 'rounded-2xl';
  }

  protected panelStyle(entry: DialogEntry): Record<string, string> {
    if (entry.config.mode === 'max') return {};
    return {
      width:     entry.config.width  ?? '32rem',
      ...(entry.config.height ? { height: entry.config.height } : {}),
    };
  }

  /**
   * Returns a stable Injector for the given dialog entry. The same instance is returned
   * on every change-detection cycle so NgComponentOutlet does not destroy and recreate
   * the content component unnecessarily.
   * @param entry The dialog entry to build an injector for.
   * @returns Cached child Injector with DIALOG_REF_TOKEN and DIALOG_DATA providers.
   */
  protected injectorFor(entry: DialogEntry): Injector {
    let cached = this.injectorCache.get(entry.id);
    if (!cached) {
      cached = Injector.create({
        parent: this.injector,
        providers: [
          { provide: 'DIALOG_DATA',    useValue: entry.config.data },
          { provide: 'DIALOG_REF',     useValue: { close: (r: unknown) => this.svc._close(entry.id, r) } },
          { provide: DIALOG_REF_TOKEN, useValue: { close: (r: unknown) => this.svc._close(entry.id, r) } },
        ],
      });
      this.injectorCache.set(entry.id, cached);
    }
    return cached;
  }
}
