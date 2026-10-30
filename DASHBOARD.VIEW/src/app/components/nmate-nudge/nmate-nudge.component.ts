import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NMateNudge } from '../../models/nmate-nudge.model';

/**
 * NMate's offer of help, as a bubble above the launcher.
 *
 * Kept apart from the widget because it is a different thing in the same corner: the widget
 * is a chat panel the reader opens, this is NMate speaking first. Separating them also keeps
 * each component's stylesheet within its budget.
 */
@Component({
  selector: 'app-nmate-nudge',
  templateUrl: './nmate-nudge.component.html',
  styleUrl: './nmate-nudge.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NMateNudgeComponent {
  /** The offer to show. */
  readonly nudge = input.required<NMateNudge>();

  /** Raised when the reader takes up the offer. */
  readonly accepted = output<NMateNudge>();

  /** Raised when the reader waves it away. */
  readonly dismissed = output<void>();
}
