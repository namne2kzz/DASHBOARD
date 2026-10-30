/**
 * Something the palette can do, as opposed to something it can find.
 *
 * Commands are what turn a search box into a command palette: typing "sprint" should be able
 * to create one, not only list the ones that already exist.
 */
export interface PaletteCommand {
  /** Stable identifier, used for tracking and tests. */
  id: string;

  /** What the command is called, e.g. "New work item". */
  label: string;

  /**
   * Extra words that should match this command without appearing in its label.
   *
   * People reach for a command by what they want to achieve, which is often not the word
   * on the button — "theme" should find "Switch to dark mode".
   */
  keywords?: string[];

  /** Single-key shortcut shown on the right, where one exists. */
  hint?: string;

  /** Inline SVG path data for the leading icon, drawn at 24×24. */
  icon: string;

  /** Runs the command. The palette closes first. */
  run: () => void;
}
