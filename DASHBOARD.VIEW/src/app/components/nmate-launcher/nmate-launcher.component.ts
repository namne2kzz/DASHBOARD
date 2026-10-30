import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NMateService } from '../../services/nmate.service';

/**
 * The floating N-bot button that opens NMate.
 *
 * Separate from the widget because it is a different object in the same corner — a mark with
 * four visual states of its own (idle, thinking, unread, open), which the panel knows nothing
 * about. It reads NMate's state directly rather than taking it as inputs, since every one of
 * those states is NMate's rather than the host's.
 */
@Component({
  selector: 'app-nmate-launcher',
  templateUrl: './nmate-launcher.component.html',
  styleUrl: './nmate-launcher.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NMateLauncherComponent {
  readonly nmate = inject(NMateService);

  /** True while an offer of help is on screen; the mark takes a coral rim to match it. */
  readonly hasNudge = input(false);

  /**
   * What the launcher announces to a screen reader.
   *
   * Its visual states — blinking, squinting inside a spinning ring, a badge — say nothing to
   * somebody who cannot see them, so each gets its own wording here.
   */
  readonly fabLabel = computed(() => {
    if (this.nmate.isOpen())      return 'Close NMate';
    if (this.nmate.isStreaming()) return 'NMate is replying — open to read it';

    const unread = this.nmate.unreadCount();
    return unread > 0
      ? `NMate has ${unread} new ${unread === 1 ? 'reply' : 'replies'} — open to read`
      : 'Open NMate — the assistant';
  });
}
