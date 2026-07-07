const UNITS: { limit: number; divisor: number; suffix: string }[] = [
  { limit: 60, divisor: 1, suffix: 's ago' },
  { limit: 3600, divisor: 60, suffix: 'm ago' },
  { limit: 86400, divisor: 3600, suffix: 'h ago' },
  { limit: 604800, divisor: 86400, suffix: 'd ago' },
  { limit: 2629800, divisor: 604800, suffix: 'w ago' },
];

/** Formats a past timestamp as a short relative-time string (e.g. "5m ago", "3d ago"). @param value ISO date string or Date. @returns Relative time, or the localized date for anything older than ~a month. */
export function timeAgo(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const seconds = Math.max(0, (Date.now() - date.getTime()) / 1000);

  if (seconds < 5) return 'just now';

  for (const unit of UNITS) {
    if (seconds < unit.limit) return `${Math.floor(seconds / unit.divisor)}${unit.suffix}`;
  }

  return date.toLocaleDateString();
}
