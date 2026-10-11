import { Component, input, output } from '@angular/core';

/**
 * The single way a page shows that its data failed to load: a danger alert with the message and,
 * when the page can reload, a "Try again" button. Action failures (save, delete…) use toasts instead.
 */
@Component({
  selector: 'app-load-error',
  templateUrl: './load-error.component.html',
  styleUrl: './load-error.component.css',
})
export class LoadErrorComponent {
  /** User-facing message — build it with describeLoadError(). */
  readonly message   = input.required<string>();
  /** Hide the retry button for errors a reload cannot fix. */
  readonly retryable = input(true);
  /** Emitted when the user asks to load again. */
  readonly retry     = output<void>();
}
