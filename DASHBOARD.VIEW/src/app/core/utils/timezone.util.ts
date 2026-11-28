/**
 * IANA timezone helpers.
 *
 * Angular's `formatDate` takes a fixed offset string (`"+0700"`), not a zone id, so a stored IANA id
 * has to be resolved to the offset **that zone had at that instant**. Resolving per instant is what
 * makes daylight saving come out right: the same Berlin event formats as +0100 in January and +0200
 * in July, which a stored offset could never do.
 */

/** Zone ids offered in the picker, newest-supported API first. */
let cachedZones: readonly string[] | null = null;

/**
 * Every IANA zone id the browser knows, sorted.
 * @returns The zone ids, or a small curated fallback when the browser cannot enumerate them.
 * @remarks
 * `Intl.supportedValuesOf` is the standard way to enumerate zones (Chrome/Edge 99+, Firefox 93+,
 * Safari 15.4+). Older engines throw or lack it, so a short fallback list keeps the picker usable
 * rather than empty. Computed once — the list is ~450 entries and never changes at runtime.
 */
export function listTimezones(): readonly string[] {
  if (cachedZones) return cachedZones;

  try {
    const supported = (Intl as unknown as { supportedValuesOf?: (k: string) => string[] })
      .supportedValuesOf?.('timeZone');
    if (supported?.length) return (cachedZones = [...supported].sort());
  } catch {
    // fall through to the curated list
  }

  return (cachedZones = FALLBACK_ZONES);
}

/**
 * The browser's own zone id.
 * @returns An IANA id such as `"Asia/Ho_Chi_Minh"`, or `"UTC"` if the browser will not say.
 */
export function browserTimezone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

/**
 * The UTC offset a zone was at a given instant, in Angular's `formatDate` form.
 * @param zone IANA zone id; empty/invalid falls back to the browser's zone.
 * @param at The instant to resolve at — defaults to now. Pass the date being formatted so DST is
 * taken from that date rather than today.
 * @returns An offset like `"+0700"` or `"-0500"`, or undefined to mean "use the browser's zone".
 */
export function offsetFor(zone: TimezoneLike, at: Date = new Date()): string | undefined {
  if (!zone) return undefined; // '' = browser local; let Angular use its default

  const minutes = offsetMinutesFor(zone, at);
  if (minutes === null) return undefined; // unknown id → degrade to browser local

  const sign = minutes < 0 ? '-' : '+';
  const abs = Math.abs(minutes);
  const pad = (n: number) => `${n}`.padStart(2, '0');
  return `${sign}${pad(Math.floor(abs / 60))}${pad(abs % 60)}`;
}

/**
 * A zone's offset from UTC in minutes at a given instant (east of UTC is positive).
 * @param zone IANA zone id.
 * @param at The instant to resolve at.
 * @returns Offset in minutes, or null when the id is not recognised.
 * @remarks
 * Derived by asking `Intl` to render the instant in that zone, reading the parts back as if they were
 * UTC, and taking the difference. That is the standard trick for getting a numeric offset out of the
 * platform's tz database without shipping one.
 */
export function offsetMinutesFor(zone: string, at: Date = new Date()): number | null {
  try {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      hour12: false,
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    }).formatToParts(at);

    const get = (type: string) => Number(parts.find(p => p.type === type)?.value);
    // 'en-US' renders midnight as hour 24; Date.UTC needs 0.
    const hour = get('hour') % 24;

    const asUtc = Date.UTC(get('year'), get('month') - 1, get('day'), hour, get('minute'), get('second'));
    // Both sides are whole seconds; the instant's own milliseconds would otherwise skew the diff.
    return Math.round((asUtc - Math.floor(at.getTime() / 1000) * 1000) / 60_000);
  } catch {
    return null; // invalid zone id
  }
}

/**
 * A human label for a zone, with its current offset.
 * @param zone IANA zone id.
 * @returns Something like `"Asia/Ho_Chi_Minh (UTC+07:00)"`.
 */
export function timezoneLabel(zone: string): string {
  const minutes = offsetMinutesFor(zone);
  if (minutes === null) return zone;

  const sign = minutes < 0 ? '-' : '+';
  const abs = Math.abs(minutes);
  const pad = (n: number) => `${n}`.padStart(2, '0');
  return `${zone.replace(/_/g, ' ')} (UTC${sign}${pad(Math.floor(abs / 60))}:${pad(abs % 60)})`;
}

/** Accepts the empty string (meaning "browser local") alongside real zone ids. */
type TimezoneLike = string | null | undefined;

/**
 * Extra search terms per zone, for cities whose common name is not in the IANA id.
 *
 * @remarks
 * IANA names the zone after one representative city, which is often not the one people type:
 * Vietnam's zone is `Asia/Ho_Chi_Minh`, so searching "hanoi" finds nothing. The aliases only widen
 * matching — they are never shown or stored.
 */
const ZONE_ALIASES: Record<string, readonly string[]> = {
  // Keyed under both the current and the legacy IANA name: engines disagree on which they list.
  // Windows/ICU enumerates `Asia/Saigon` and `Asia/Calcutta`, while others use the newer names —
  // so a map keyed on one spelling silently matches nothing on half the machines.
  'Asia/Ho_Chi_Minh': ['hanoi', 'saigon', 'vietnam', 'viet nam'],
  'Asia/Saigon':      ['hanoi', 'ho chi minh', 'vietnam', 'viet nam'],
  'Asia/Calcutta':    ['india', 'mumbai', 'delhi', 'bangalore', 'kolkata'],
  'Asia/Kolkata':     ['india', 'mumbai', 'delhi', 'bangalore', 'calcutta'],
  'Asia/Shanghai':    ['china', 'beijing'],
  'Asia/Tokyo':       ['japan'],
  'Asia/Seoul':       ['korea'],
  'Asia/Bangkok':     ['thailand'],
  'Asia/Jakarta':     ['indonesia'],
  'Asia/Manila':      ['philippines'],
  'Asia/Singapore':   ['singapore'],
  'Asia/Dubai':       ['uae', 'emirates', 'abu dhabi'],
  'Europe/London':    ['uk', 'britain', 'england', 'gmt', 'bst'],
  'Europe/Paris':     ['france'],
  'Europe/Berlin':    ['germany', 'munich', 'frankfurt'],
  'Europe/Madrid':    ['spain'],
  'Europe/Rome':      ['italy'],
  'Europe/Amsterdam': ['netherlands', 'holland'],
  'Europe/Moscow':    ['russia'],
  'America/New_York': ['usa', 'us east', 'eastern', 'est', 'edt', 'boston', 'washington'],
  'America/Chicago':  ['central', 'cst', 'cdt', 'texas', 'dallas'],
  'America/Denver':   ['mountain', 'mst', 'mdt'],
  'America/Los_Angeles': ['pacific', 'pst', 'pdt', 'california', 'san francisco', 'seattle'],
  'America/Sao_Paulo': ['brazil'],
  'Australia/Sydney': ['australia', 'nsw'],
  'Pacific/Auckland': ['new zealand', 'nz'],
  UTC: ['gmt', 'utc', 'zulu'],
};

/**
 * Whether a zone matches a free-text query, across its id, its label and its aliases.
 * @param zone IANA zone id.
 * @param query Lower-cased search text; empty matches everything.
 * @returns True when the zone should stay in the filtered list.
 */
/**
 * Ensures a saved zone appears in a list, even when the engine enumerates it under another name.
 * @param zones The zones the browser enumerated.
 * @param saved The currently saved zone id; empty means "follow the device".
 * @returns The list, with `saved` prepended when it is valid but missing.
 * @remarks
 * IANA renames zones and keeps the old name as an alias (`Asia/Saigon` → `Asia/Ho_Chi_Minh`), and
 * engines differ on which they list. Without this, a value saved on one machine vanishes from the
 * dropdown on another — the select renders blank and the next save silently changes the user's zone.
 */
export function withSavedZone(zones: readonly string[], saved: string): readonly string[] {
  if (!saved || zones.includes(saved)) return zones;
  // Only keep it if the engine actually understands it; a typo should not become an option.
  return offsetMinutesFor(saved) === null ? zones : [saved, ...zones];
}

export function zoneMatches(zone: string, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;

  // Underscores are how IANA writes spaces, so "ho chi" should match "Ho_Chi_Minh".
  const id = zone.toLowerCase();
  if (id.includes(q) || id.replace(/_/g, ' ').includes(q)) return true;
  if (timezoneLabel(zone).toLowerCase().includes(q)) return true;

  return (ZONE_ALIASES[zone] ?? []).some(alias => alias.includes(q));
}

/**
 * Used only when the browser cannot enumerate zones. Covers the common business hubs rather than
 * pretending to be complete — the picker also accepts whatever the saved value already is.
 */
const FALLBACK_ZONES: readonly string[] = [
  'UTC',
  'Africa/Cairo', 'Africa/Johannesburg', 'Africa/Lagos',
  'America/Bogota', 'America/Chicago', 'America/Denver', 'America/Los_Angeles',
  'America/Mexico_City', 'America/New_York', 'America/Sao_Paulo', 'America/Toronto',
  'Asia/Bangkok', 'Asia/Dubai', 'Asia/Ho_Chi_Minh', 'Asia/Hong_Kong', 'Asia/Jakarta',
  'Asia/Kolkata', 'Asia/Manila', 'Asia/Seoul', 'Asia/Shanghai', 'Asia/Singapore',
  'Asia/Taipei', 'Asia/Tokyo',
  'Australia/Melbourne', 'Australia/Perth', 'Australia/Sydney',
  'Europe/Amsterdam', 'Europe/Berlin', 'Europe/Dublin', 'Europe/Istanbul',
  'Europe/Lisbon', 'Europe/London', 'Europe/Madrid', 'Europe/Moscow',
  'Europe/Paris', 'Europe/Rome', 'Europe/Stockholm', 'Europe/Warsaw', 'Europe/Zurich',
  'Pacific/Auckland', 'Pacific/Honolulu',
];
