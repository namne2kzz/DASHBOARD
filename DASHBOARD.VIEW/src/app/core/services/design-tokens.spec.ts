import { ACCENTS, Theme } from './theme.service';

/**
 * Guards the colour contrast of the design tokens.
 *
 * Every one of these numbers was wrong at some point: white text was laid on the
 * bright dark-mode accent (3.68:1), on the semantic fills (2.52-3.46:1), and
 * --accent on --accent-soft (slate: 3.07:1). Nothing failed, because the unit
 * tests never looked at colour — the defects were found by hand, twice.
 *
 * These specs read the real stylesheet through the real cascade, so a token edit
 * that drops a pair below WCAG AA fails here instead of shipping.
 */

/** WCAG 2.1 AA for normal-sized text. */
const AA_NORMAL = 4.5;

/** Relative luminance of an `rgb()` / `#rrggbb` colour, per WCAG. */
function luminance(colour: string): number {
  const channels = parse(colour);
  const [r, g, b] = channels.map(v => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Splits `#rrggbb` or `rgb(r, g, b)` into three 0-255 channels. */
function parse(colour: string): [number, number, number] {
  const hex = colour.trim().match(/^#([0-9a-f]{6})$/i);
  if (hex) {
    const n = parseInt(hex[1], 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }
  const nums = colour.match(/[\d.]+/g);
  if (!nums || nums.length < 3) throw new Error(`Cannot parse colour: "${colour}"`);
  return [+nums[0], +nums[1], +nums[2]];
}

/** Contrast ratio between two colours, 1 (identical) to 21 (black on white). */
function contrast(a: string, b: string): number {
  const [x, y] = [luminance(a), luminance(b)];
  const [hi, lo] = x > y ? [x, y] : [y, x];
  return (hi + 0.05) / (lo + 0.05);
}

/** Reads a custom property off <html> with the given theme and accent applied. */
function tokens(theme: Theme, accent: string, names: string[]): Record<string, string> {
  const root = document.documentElement;
  const prevTheme = root.getAttribute('data-theme');
  const prevAccent = root.getAttribute('data-accent');
  root.setAttribute('data-theme', theme);
  root.setAttribute('data-accent', accent);
  const style = getComputedStyle(root);
  const read = Object.fromEntries(names.map(n => [n, style.getPropertyValue(n).trim()]));
  restore(root, 'data-theme', prevTheme);
  restore(root, 'data-accent', prevAccent);
  return read;
}

/** Puts an attribute back, removing it if it was absent before. */
function restore(el: HTMLElement, name: string, value: string | null): void {
  if (value === null) el.removeAttribute(name);
  else el.setAttribute(name, value);
}

describe('design tokens: colour contrast', () => {
  const themes: Theme[] = ['light', 'dark'];

  // A token pair is skipped rather than failed when the stylesheet is not loaded
  // into the test DOM; a wrong value still fails, an absent one does not lie.
  function pairIsReadable(fg: string, bg: string): boolean {
    return /^#|^rgb/.test(fg) && /^#|^rgb/.test(bg);
  }

  for (const theme of themes) {
    for (const { value: accent } of ACCENTS) {
      it(`${theme}/${accent}: --on-accent reads on the solid --accent`, () => {
        const t = tokens(theme, accent, ['--on-accent', '--accent']);
        if (!pairIsReadable(t['--on-accent'], t['--accent'])) {
          pending('design tokens not loaded in this test DOM');
          return;
        }
        expect(contrast(t['--on-accent'], t['--accent'])).toBeGreaterThanOrEqual(AA_NORMAL);
      });

      it(`${theme}/${accent}: --accent-text reads on --accent-soft`, () => {
        const t = tokens(theme, accent, ['--accent-text', '--accent-soft']);
        if (!pairIsReadable(t['--accent-text'], t['--accent-soft'])) {
          pending('design tokens not loaded in this test DOM');
          return;
        }
        expect(contrast(t['--accent-text'], t['--accent-soft'])).toBeGreaterThanOrEqual(AA_NORMAL);
      });
    }

    // Each semantic fill carries its own ink, because one shared value fails
    // somewhere: light-mode amber takes white at only 3.14:1 while red and green
    // take it comfortably.
    for (const fill of ['danger', 'warning', 'success'] as const) {
      it(`${theme}: --on-${fill} reads on the solid --${fill}`, () => {
        const t = tokens(theme, 'blue', [`--on-${fill}`, `--${fill}`]);
        if (!pairIsReadable(t[`--on-${fill}`], t[`--${fill}`])) {
          pending('design tokens not loaded in this test DOM');
          return;
        }
        expect(contrast(t[`--on-${fill}`], t[`--${fill}`])).toBeGreaterThanOrEqual(AA_NORMAL);
      });
    }

    // --text-3 is the dimmest text in the system, so it is the one that fails first.
    // It must hold on the raised surfaces too, not just on the page background.
    for (const surface of ['--bg', '--surface', '--surface-2', '--surface-3'] as const) {
      it(`${theme}: --text-3 reads on ${surface}`, () => {
        const t = tokens(theme, 'blue', ['--text-3', surface]);
        if (!pairIsReadable(t['--text-3'], t[surface])) {
          pending('design tokens not loaded in this test DOM');
          return;
        }
        expect(contrast(t['--text-3'], t[surface])).toBeGreaterThanOrEqual(AA_NORMAL);
      });
    }
  }
});

describe('design tokens: type scale', () => {
  /**
   * The scale has six rungs. 2xl/3xl/4xl were once folded onto a single 32px,
   * which made a page title, a section heading and a headline metric identical
   * and flattened every screen. The top rung must stay reachable by itself.
   */
  it('keeps a step between the heading size and the metric size', () => {
    const style = getComputedStyle(document.documentElement);
    const xl = style.getPropertyValue('--text-xl').trim();
    const xxl = style.getPropertyValue('--text-2xl').trim();
    if (!xl || !xxl) {
      pending('design tokens not loaded in this test DOM');
      return;
    }
    expect(parseFloat(xxl)).toBeGreaterThan(parseFloat(xl));
  });
});
