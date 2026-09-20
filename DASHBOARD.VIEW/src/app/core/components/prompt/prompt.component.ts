import { Component, Inject, signal, ElementRef, AfterViewInit, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * Options for the shared prompt (text-input) dialog.
 * Used when a single-line string is needed from the user before proceeding.
 */
export interface PromptOptions {
  /** Dialog heading. */
  title: string;
  /** Label displayed above the text input. */
  label?: string;
  /** Pre-filled value in the input. */
  defaultValue?: string;
  /** Placeholder for the empty input. */
  placeholder?: string;
  /** Confirm button label. Default: 'OK'. */
  confirmText?: string;
  /** Cancel button label. Default: 'Cancel'. */
  cancelText?: string;
}

@Component({
  selector: 'app-prompt',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './prompt.component.html',
  styleUrl: './prompt.component.scss',
})
export class PromptComponent implements AfterViewInit {
  protected readonly options: PromptOptions;
  private  readonly dialogRef: { close(r: unknown): void };

  protected readonly value = signal('');

  /** Reference to the input element for auto-focus. */
  private readonly inputRef = viewChild<ElementRef<HTMLInputElement>>('inputEl');

  constructor(
    @Inject('DIALOG_DATA') data: PromptOptions,
    @Inject('DIALOG_REF')  ref: { close(r: unknown): void },
  ) {
    this.options   = data ?? { title: 'Input' };
    this.dialogRef = ref;
    this.value.set(data?.defaultValue ?? '');
  }

  ngAfterViewInit(): void {
    const input = this.inputRef()?.nativeElement;
    if (input) {
      input.focus();
      input.select();
    }
  }

  /** Resolved confirm label. @returns Confirm button text. */
  protected get confirmText(): string { return this.options.confirmText ?? 'OK'; }

  /** Resolved cancel label. @returns Cancel button text. */
  protected get cancelText(): string { return this.options.cancelText ?? 'Cancel'; }

  /** Submits the current value (or null if empty) and closes. */
  protected confirm(): void {
    const trimmed = this.value().trim();
    this.dialogRef.close(trimmed || null);
  }

  /** Cancels and closes, resolving to null. */
  protected cancel(): void { this.dialogRef.close(null); }

  /** Allows submitting via Enter key. @param event Keyboard event. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') { event.preventDefault(); this.confirm(); }
    if (event.key === 'Escape') { event.preventDefault(); this.cancel(); }
  }
}
