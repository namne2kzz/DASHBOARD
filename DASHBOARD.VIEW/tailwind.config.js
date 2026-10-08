/**
 * Nexus UI Foundation v1 — the Tailwind palette is remapped onto the design
 * tokens declared in src/styles.css (`--c-*` RGB channels). Templates keep
 * their existing utilities (bg-slate-900, text-sky-400, …) while every colour
 * resolves to a token that switches per theme via html[data-theme].
 *
 * Mapping is per utility so a single shade can mean different roles:
 *   bg-slate-800 → surface-2, border-slate-800 → border, text-slate-400 → text-2.
 */

/** Token reference that keeps Tailwind opacity modifiers (`/10`, `/50`) working. */
const t = (name) => `rgb(var(--c-${name}) / <alpha-value>)`;

/** Builds a full 50–950 scale from a resolver. */
const scale = (pick) =>
  Object.fromEntries([50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950].map((s) => [s, pick(s)]));

/** Semantic hue: deep/pale ends become the soft tint, the rest the solid colour. */
const semantic = (name) => scale((s) => (s <= 100 || s >= 900 ? t(`${name}-soft`) : t(name)));

/** Accent hue: soft ends, hover shade for 400/600/700 backgrounds. */
const accent = scale((s) => (s <= 100 || s >= 900 ? t('accent-soft') : t('accent')));
const accentBg = scale((s) =>
  s <= 100 || s >= 900 ? t('accent-soft') : [400, 600, 700].includes(s) ? t('accent-hover') : t('accent'),
);

/** Neutral roles — default for ring/divide/gradient/placeholder/etc. */
const slate = scale((s) =>
  s <= 200 ? t('text') : s <= 400 ? t('text-2') : s <= 600 ? t('text-3') : s === 700 ? t('border-strong') : s === 800 ? t('border') : s === 900 ? t('surface') : t('bg'),
);

const slateBg = scale((s) =>
  s <= 200 ? t('text') : s === 300 ? t('text-2') : s <= 500 ? t('text-3') : s === 600 ? t('border-strong') : s === 700 ? t('surface-3') : s === 800 ? t('surface-2') : s === 900 ? t('surface') : t('bg'),
);

const slateBorder = scale((s) =>
  s <= 400 ? t('text-2') : s === 500 ? t('text-3') : s <= 700 ? t('border-strong') : s === 950 ? t('bg') : t('border'),
);

const slateText = scale((s) =>
  s <= 200 ? t('text') : s <= 400 ? t('text-2') : s <= 600 ? t('text-3') : s === 700 ? t('border-strong') : s === 950 ? t('on-accent') : t('text'),
);

const success = semantic('success');
const warning = semantic('warning');
const danger = semantic('danger');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        slate,
        sky: accent,
        indigo: accent,
        emerald: success,
        green: success,
        teal: success,
        amber: warning,
        orange: warning,
        yellow: warning,
        rose: danger,
        red: danger,
        pink: danger,
        lime: success,
        // Hues outside the foundation fold into the type identifiers they stand for.
        violet: scale(() => t('type-test')),
        purple: scale(() => t('type-test')),
        fuchsia: scale(() => t('type-test')),
        blue: scale(() => t('type-story')),
        cyan: scale(() => t('type-story')),
        // Work-item type identifiers (8px square next to the ID).
        type: {
          story: t('type-story'),
          task: t('type-task'),
          bug: t('type-bug'),
          test: t('type-test'),
          improvement: t('type-improvement'),
        },
        // Direct token access for new markup: bg-nx-surface, text-nx-text-2, …
        nx: Object.fromEntries(
          ['bg', 'surface', 'surface-2', 'surface-3', 'border', 'border-strong', 'text', 'text-2', 'text-3',
            'accent', 'accent-hover', 'accent-soft', 'on-accent', 'success', 'success-soft',
            'warning', 'warning-soft', 'danger', 'danger-soft'].map((n) => [n, t(n)]),
        ),
      },
      backgroundColor: { slate: slateBg, sky: accentBg, indigo: accentBg },
      borderColor: { slate: slateBorder, white: t('text') },
      divideColor: { slate: slateBorder },
      textColor: { slate: slateText, white: t('text') },
      ringColor: { white: t('text') },
      fontFamily: {
        sans: ['"IBM Plex Sans"', 'system-ui', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'monospace'],
      },
      // Density of a work tool: body 13px, label/meta 12px, title 15px, display 20px.
      fontSize: {
        xs: ['12px', '16px'],
        sm: ['13px', '20px'],
        base: ['14px', '22px'],
        lg: ['15px', '22px'],
        xl: ['18px', '26px'],
        '2xl': ['20px', '28px'],
        '3xl': ['24px', '32px'],
      },
      // 4 badge/tag · 6 control/card · 8 panel.
      borderRadius: {
        sm: '4px',
        DEFAULT: '4px',
        md: '6px',
        lg: '6px',
        xl: '6px',
        '2xl': '8px',
        '3xl': '8px',
      },
      // 1px borders instead of shadows; elevation only for menus and dialogs.
      boxShadow: {
        sm: 'none',
        DEFAULT: 'none',
        md: 'none',
        inner: 'none',
        lg: 'var(--shadow-pop)',
        xl: 'var(--shadow-pop)',
        '2xl': 'var(--shadow-pop)',
      },
    },
  },
  plugins: [],
};
