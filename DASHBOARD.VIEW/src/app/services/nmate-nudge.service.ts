import { Injectable, signal } from '@angular/core';
import { NMateNudge } from '../models/nmate-nudge.model';

/**
 * Holds the contextual offer of help currently on screen, if any.
 *
 * Screens publish a nudge when they are showing a problem NMate could act on; the widget
 * renders whatever is here. Keeping it in a service rather than in the widget means the
 * widget needs no knowledge of Sprint, Board or any other screen, and a screen needs no
 * reference to the widget.
 *
 * Each nudge is shown at most once per session. An assistant that repeats itself every time
 * you return to a screen teaches people to dismiss it unread, which costs more than the
 * occasional missed offer.
 */
@Injectable({ providedIn: 'root' })
export class NMateNudgeService {
  /** The nudge to show, or null. */
  readonly current = signal<NMateNudge | null>(null);

  /** Keys already shown or dismissed in this session. */
  private readonly seen = new Set<string>();

  /**
   * Offers help, unless this exact nudge has already been shown this session.
   *
   * @param nudge The offer to raise.
   * @returns True when it was raised, false when it was suppressed as already seen.
   */
  offer(nudge: NMateNudge): boolean {
    if (this.seen.has(nudge.key)) return false;

    this.seen.add(nudge.key);
    this.current.set(nudge);
    return true;
  }

  /** Takes the current nudge off screen. It will not be raised again this session. */
  dismiss(): void {
    this.current.set(null);
  }

  /**
   * Clears the current nudge without marking anything seen.
   *
   * Used when navigating away: the offer is about the screen being left, so it should not
   * follow the reader, but it may legitimately be raised again on returning.
   *
   * @param key The nudge key to forget, so it can be offered again.
   */
  withdraw(key: string): void {
    this.seen.delete(key);
    if (this.current()?.key === key) this.current.set(null);
  }
}
