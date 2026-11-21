import { Component, effect, ElementRef, HostListener, inject, Injector } from '@angular/core';
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
  private   readonly host     = inject(ElementRef<HTMLElement>);

  /** Cached per-dialog injectors keyed by entry ID — prevents NgComponentOutlet from recreating the component on every CD cycle. */
  private readonly injectorCache = new Map<number, Injector>();

  /** What had focus before the first dialog opened, so it can be handed back on close. */
  private returnFocusTo: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const activeIds = new Set(this.svc.dialogs().map(d => d.id));
      for (const id of this.injectorCache.keys()) {
        if (!activeIds.has(id)) this.injectorCache.delete(id);
      }
    });

    // Opening a dialog without moving focus leaves a keyboard user behind it, tabbing
    // through the page they can no longer see. Remember where they were, pull focus in,
    // and hand it back when the last dialog closes.
    effect(() => {
      const open = this.svc.dialogs().length > 0;
      if (open) {
        this.returnFocusTo ??= document.activeElement as HTMLElement | null;
        queueMicrotask(() => this.focusFirst());
      } else if (this.returnFocusTo) {
        this.returnFocusTo.focus?.();
        this.returnFocusTo = null;
      }
    });
  }

  /** Moves focus to the first control inside the topmost dialog, or to the panel itself. */
  private focusFirst(): void {
    const panel = this.topPanel();
    if (!panel) return;
    const first = panel.querySelector<HTMLElement>(
      'input:not([type="hidden"]), textarea, select, button, a[href], [tabindex]:not([tabindex="-1"])',
    );
    if (first) { first.focus(); return; }
    panel.tabIndex = -1;
    panel.focus();
  }

  /** The panel element of the dialog stacked on top. */
  private topPanel(): HTMLElement | null {
    const panels = (this.host.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[role="dialog"]');
    return panels.length ? panels[panels.length - 1] : null;
  }

  /** Escape closes the topmost closable dialog. */
  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    const list = this.svc.dialogs();
    const top  = list[list.length - 1];
    if (top?.config.closable) this.svc._close(top.id);
  }

  /**
   * Keeps Tab inside the topmost dialog. Without this the next Tab leaves the dialog and
   * walks the page behind it, which the backdrop hides but the keyboard still reaches.
   * @param ev The keydown event.
   */
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(ev: KeyboardEvent): void {
    const panel = this.topPanel();
    if (!panel) return;
    const focusable = [...panel.querySelectorAll<HTMLElement>(
      'input:not([type="hidden"]):not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])',
    )].filter(el => el.offsetWidth > 0 || el.offsetHeight > 0);
    if (!focusable.length) return;

    const first = focusable[0];
    const last  = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;

    if (!panel.contains(active)) { ev.preventDefault(); first.focus(); return; }
    if (ev.shiftKey && active === first) { ev.preventDefault(); last.focus(); }
    else if (!ev.shiftKey && active === last) { ev.preventDefault(); first.focus(); }
  }

  /** Stable id for a dialog's heading, used by aria-labelledby. @param entry The dialog entry. */
  protected titleId(entry: DialogEntry): string { return `nx-dialog-title-${entry.id}`; }

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
