import { inject, Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ResourceService } from '../../services/resource.service';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly toastr = inject(ToastrService);
  private readonly rs     = inject(ResourceService);

  /** @param message Body text. @param title Optional title override. */
  success(message: string, title?: string): void {
    this.toastr.success(message, title ?? this.rs.get('toast.success'));
  }

  /**
   * An error stays until it is closed.
   *
   * A failure that auto-dismissed while the reader was looking elsewhere is a failure they
   * never learn about, and the next thing they do is built on a wrong assumption.
   *
   * @param message Body text.
   * @param title Optional title override.
   */
  error(message: string, title?: string): void {
    this.toastr.error(message, title ?? this.rs.get('toast.error'), {
      timeOut: 0,
      extendedTimeOut: 0,
    });
  }

  /**
   * Confirms an action and offers to take it back.
   *
   * Offered for changes that are easy to make by accident — dragging a card into the wrong
   * column, say — where asking first would be heavier than the mistake. The toast lives a
   * little longer than usual, because reading it and deciding takes longer than reading it.
   *
   * @param message What happened, e.g. "Moved DASH-64 to In Progress".
   * @param undo Called when the reader takes the action back.
   */
  undoable(message: string, undo: () => void): void {
    const toast = this.toastr.success(message, undefined, {
      timeOut: 8000,
      closeButton: true,
      tapToDismiss: false,
    });

    // onTap fires for a click anywhere on the toast body, which is the whole affordance
    // here: the toast says what happened and clicking it puts things back.
    toast?.onTap.subscribe(() => undo());
  }

  /** @param message Body text. @param title Optional title override. */
  warning(message: string, title?: string): void {
    this.toastr.warning(message, title ?? this.rs.get('toast.warning'));
  }

  /** @param message Body text. @param title Optional title override. */
  info(message: string, title?: string): void {
    this.toastr.info(message, title ?? this.rs.get('toast.info'));
  }
}
