import { Component, Inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { ResourcePipe } from '../../pipes/resource.pipe';
import { ResourceKey } from '../../services/resource.service';

export type ConfirmType = 'danger' | 'warning' | 'info' | 'success';

export interface ConfirmOptions {
  titleKey?:      ResourceKey;
  subject?:       string;
  messageKey?:    ResourceKey;
  messageParams?: Record<string, string | number>;
  confirmKey?:    ResourceKey;
  cancelKey?:     ResourceKey;
  type?:          ConfirmType;
}

@Component({
  selector: 'app-confirm',
  standalone: true,
  imports: [ResourcePipe, NgClass],
  templateUrl: './confirm.component.html',
  styleUrl: './confirm.component.scss',
})
export class ConfirmComponent {
  protected readonly options: ConfirmOptions;
  private  readonly dialogRef: { close(r: unknown): void };

  constructor(
    @Inject('DIALOG_DATA') data: ConfirmOptions,
    @Inject('DIALOG_REF')  ref: { close(r: unknown): void },
  ) {
    this.options   = data ?? {};
    this.dialogRef = ref;
  }

  protected get type(): ConfirmType       { return this.options.type      ?? 'danger'; }
  protected get titleKey(): ResourceKey   { return this.options.titleKey  ?? 'confirm.deleteTitle'; }
  protected get msgKey(): ResourceKey     { return this.options.messageKey ?? 'confirm.deleteMessage'; }
  protected get confirmKey(): ResourceKey { return this.options.confirmKey ?? 'confirm.yes'; }
  protected get cancelKey(): ResourceKey  { return this.options.cancelKey  ?? 'confirm.no'; }

  protected get iconClass(): string {
    return {
      danger:  'bg-rose-500/15    ring-rose-500/30    text-rose-400',
      warning: 'bg-amber-500/15   ring-amber-500/30   text-amber-400',
      info:    'bg-sky-500/15     ring-sky-500/30     text-sky-400',
      success: 'bg-emerald-500/15 ring-emerald-500/30 text-emerald-400',
    }[this.type];
  }

  protected get confirmBtnClass(): string {
    return {
      danger:  'bg-rose-500    hover:bg-rose-400',
      warning: 'bg-amber-500   hover:bg-amber-400',
      info:    'bg-sky-500     hover:bg-sky-400',
      success: 'bg-emerald-500 hover:bg-emerald-400',
    }[this.type];
  }

  /** Closes the dialog resolving to true (user confirmed). */
  protected confirm(): void { this.dialogRef.close(true); }

  /** Closes the dialog resolving to false (user cancelled). */
  protected cancel(): void  { this.dialogRef.close(false); }
}
