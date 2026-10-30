/**
 * A contextual offer of help, raised beside the NMate launcher.
 *
 * Built from something already visible on the screen — a column over its WIP limit, a person
 * carrying too much — so the offer is specific rather than a generic invitation to chat.
 */
export interface NMateNudge {
  /**
   * Identifies the nudge for the once-per-session rule.
   *
   * Includes what it is about, not just the screen, so a different problem on the same screen
   * can still raise one while the same problem cannot nag twice.
   */
  key: string;

  /** The sentence shown in the bubble. */
  message: string;

  /** Label on the accept button, e.g. "Rebalance". */
  action: string;

  /** Question sent to NMate when the offer is accepted. */
  question: string;
}
