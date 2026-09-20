import { Component, inject, Inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { ResourceService, ResourceKey } from '../../services/resource.service';

export type ConfirmType = 'danger' | 'warning' | 'info' | 'success';

/**
 * Options for the shared confirm dialog.
 * Direct-string fields (`title`, `description`, `confirmText`, `cancelText`) take
 * precedence over their ResourceKey counterparts when both are provided.
 */
export interface ConfirmOptions {
  // ── Direct-string mode (preferred for dynamic content) ───────────────────
  /** Dialog heading. Overrides `titleKey` when provided. */
  title?:       string;
  /** Body copy. Overrides `messageKey` when provided. */
  description?: string;
  /** Confirm button label. Overrides `confirmKey` when provided. */
  confirmText?: string;
  /** Cancel button label. Overrides `cancelKey` when provided. */
  cancelText?:  string;

  // ── Resource-key mode (legacy / i18n) ────────────────────────────────────
  titleKey?:      ResourceKey;
  /** Optional entity name shown bold before the message body. */
  subject?:       string;
  messageKey?:    ResourceKey;
  messageParams?: Record<string, string | number>;
  confirmKey?:    ResourceKey;
  cancelKey?:     ResourceKey;

  /** Visual tone — controls icon colour and action button colour. Default: 'danger'. */
  type?: ConfirmType;
}

@Component({
  selector: 'app-confirm',
  standalone: true,
  imports: [NgClass],
  templateUrl: './confirm.component.html',
  styleUrl: './confirm.component.scss',
})
export class ConfirmComponent {
  protected readonly options: ConfirmOptions;
  private  readonly dialogRef: { close(r: unknown): void };
  private  readonly rs = inject(ResourceService);

  constructor(
    @Inject('DIALOG_DATA') data: ConfirmOptions,
    @Inject('DIALOG_REF')  ref: { close(r: unknown): void },
  ) {
    this.options   = data ?? {};
    this.dialogRef = ref;
  }

  // ── Resolved display strings ─────────────────────────────────────────────

  /** Resolved heading text. @returns Direct title or resource-key lookup. */
  protected get titleDisplay(): string {
    if (this.options.title) return this.options.title;
    return this.rs.get(this.options.titleKey ?? 'confirm.deleteTitle');
  }

  /** Resolved body text. @returns Direct description or resource-key lookup. */
  protected get descDisplay(): string {
    if (this.options.description) return this.options.description;
    const key = this.options.messageKey ?? 'confirm.deleteMessage';
    return this.rs.get(key, this.options.messageParams);
  }

  /** Resolved confirm-button label. @returns Direct confirmText or resource-key lookup. */
  protected get confirmDisplay(): string {
    if (this.options.confirmText) return this.options.confirmText;
    return this.rs.get(this.options.confirmKey ?? 'confirm.yes');
  }

  /** Resolved cancel-button label. @returns Direct cancelText or resource-key lookup. */
  protected get cancelDisplay(): string {
    if (this.options.cancelText) return this.options.cancelText;
    return this.rs.get(this.options.cancelKey ?? 'confirm.no');
  }

  // ── Tone helpers ─────────────────────────────────────────────────────────

  /** Visual tone. @returns Resolved ConfirmType. */
  protected get type(): ConfirmType { return this.options.type ?? 'danger'; }

  /** Tailwind classes for the icon circle. @returns CSS class string. */
  protected get iconClass(): string {
    return {
      danger:  'bg-rose-500/15    ring-rose-500/30    text-rose-400',
      warning: 'bg-amber-500/15   ring-amber-500/30   text-amber-400',
      info:    'bg-sky-500/15     ring-sky-500/30     text-sky-400',
      success: 'bg-emerald-500/15 ring-emerald-500/30 text-emerald-400',
    }[this.type];
  }

  /** Tailwind classes for the confirm action button. @returns CSS class string. */
  protected get confirmBtnClass(): string {
    return {
      danger:  'bg-rose-500    hover:bg-rose-400    text-white',
      warning: 'bg-amber-500   hover:bg-amber-400   text-white',
      info:    'bg-sky-500     hover:bg-sky-400     text-slate-950',
      success: 'bg-emerald-500 hover:bg-emerald-400 text-slate-950',
    }[this.type];
  }

  /** Closes the dialog resolving to true (user confirmed). */
  protected confirm(): void { this.dialogRef.close(true); }

  /** Closes the dialog resolving to false (user cancelled). */
  protected cancel(): void  { this.dialogRef.close(false); }
}
